import { useEffect, useState, useCallback, useMemo } from 'react';
import { Table, Button, Space, Input, Modal, Form, Select, message, Upload, DatePicker } from 'antd';
import { PlusOutlined, UploadOutlined, DeleteOutlined, SearchOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import * as XLSX from 'xlsx';
import { useAuthStore } from '../../store/authStore';
import apiClient from '../../api/client';
import { getDrugDropdown } from '../../api/drugs';
import { getCustomerDropdown } from '../../api/customers';
import { getDistribution, recordDownstream, deleteDistribution } from '../../api/distribution';
import RowActions from '../../components/RowActions';
import TableFilters from '../../components/TableFilters';
import { isDateInRange, type DateRangeValue } from '../../utils/dateRange';
import { applyTableFilters, type TableFilterField, type TableFilterValues } from '../../utils/tableFilters';
import { numberSorter, textSorter } from '../../utils/tableSorters';
import type { DistributionRecordDto, DropdownItem } from '../../types';

export default function PurchaseListPage() {
  const canWrite = useAuthStore(s => s.canWrite);
  const [data, setData] = useState<DistributionRecordDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchCustomer, setSearchCustomer] = useState('');
  const [sqCustomer, setSqCustomer] = useState('');
  const [dateRange, setDateRange] = useState<DateRangeValue>(null);
  const [tableFilters, setTableFilters] = useState<TableFilterValues>({});
  const [modalOpen, setModalOpen] = useState(false);
  const [drugs, setDrugs] = useState<DropdownItem[]>([]);
  const [customers, setCustomers] = useState<DropdownItem[]>([]);
  const [form] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getDistribution({ page: 0, size: 99999, fromSelf: true });
      setData(res.items);
    } finally { setLoading(false); }
  }, []);

  const loadMeta = async () => {
    const [d, c] = await Promise.all([getDrugDropdown(), getCustomerDropdown('一级经销商')]);
    setDrugs(d); setCustomers(c);
  };

  useEffect(() => { load(); loadMeta(); }, []);

  const handleAdd = () => {
    form.resetFields();
    form.setFieldsValue({ batchNo: 'B' + dayjs().format('YYYYMMDD'), quantity: 1, distributionDate: dayjs() });
    setModalOpen(true);
  };

  const handleDelete = async (id: string) => { await deleteDistribution(id); message.success('已删除'); load(); };

  const handleOk = async () => {
    const values = await form.validateFields();
    const selectedDrug = drugs.find(d => d.id === values.drugId);
    const selectedCust = customers.find(c => c.id === values.customerId);
    await recordDownstream({
      drugId: values.drugId,
      drugName: (selectedDrug?.name || '').split(' [')[0],
      batchNo: values.batchNo,
      toCustomerId: values.customerId,
      toCustomerName: selectedCust?.name,
      quantity: parseFloat(values.quantity) || 0,
      taxedUnitPrice: parseFloat(values.taxedUnitPrice) || 0,
      salesPerson: values.salesPerson || '',
      deliveryOrderNo: values.deliveryOrderNo || '',
      distributionDate: values.distributionDate ? dayjs(values.distributionDate).format('YYYY-MM-DD') : '',
    });
    message.success('出货记录已保存'); setModalOpen(false); load();
  };

  // Excel import
  const handleExcelImport = async (file: File) => {
    message.loading('正在解析 Excel...', 0);
    const reader = new FileReader();
    reader.onload = async (e) => {
      try {
        const wb = XLSX.read(e.target?.result, { type: 'array' });
        const rows = XLSX.utils.sheet_to_json<Record<string, string>>(wb.Sheets[wb.SheetNames[0]], { defval: '' });
        message.destroy();
        if (rows.length === 0) { message.warning('Excel 无数据'); return; }

      const headers = Object.keys(rows[0]);
      const findCol = (aliases: string[]) => { for (const h of headers) { if (aliases.includes(h.trim())) return h; } return null; };
      const drugCol = findCol(['药品名称','药品','药品名']);
      const specCol = findCol(['规格型号','规格']);
      const batchCol = findCol(['批号','批次号']);
      const custCol = findCol(['采购经销商','经销商名称','经销商','一级经销商']);
      const qtyCol = findCol(['数量','采购数量','出货数量']);
      const priceCol = findCol(['含税单价','单价']);
      const salesCol = findCol(['业务员','销售员']);
      const deliveryCol = findCol(['发货单号','出货单号']);
      const dateCol = findCol(['出货日期','日期']);

      if (!drugCol) { message.error('未找到"药品名称"列'); return; }
      if (!custCol) { message.error('未找到"采购经销商"列'); return; }

      const [allDrugs, allCusts] = await Promise.all([getDrugDropdown(), getCustomerDropdown('一级经销商')]);
      const dtos = []; let skipped = 0;
      for (const row of rows) {
        const drugName = (row[drugCol] || '').trim();
        const spec = specCol ? (row[specCol] || '').trim() : '';
        const custName = (row[custCol] || '').trim();
        if (!drugName || !custName) { skipped++; continue; }

        // 精确匹配：名称+规格，如无规格则仅匹配名称
        let drug = spec ? allDrugs.find(d => {
          const name = d.name.split(' [')[0];
          const dSpec = d.name.split(' [')[1]?.replace(']', '') || '';
          return name === drugName && dSpec === spec;
        }) : null;
        if (!drug) drug = allDrugs.find(d => d.name.split(' [')[0] === drugName);
        const cust = allCusts.find(c => c.name.includes(custName));
        if (!drug || !cust) { skipped++; continue; }

        dtos.push({
          drugId: drug.id, drugName: drug.name.split(' [')[0],
          batchNo: batchCol ? (row[batchCol] || '').trim() : '',
          toCustomerId: cust.id, toCustomerName: cust.name,
          quantity: qtyCol ? (parseFloat(row[qtyCol]) || 1) : 1,
          taxedUnitPrice: priceCol ? (parseFloat(row[priceCol]) || 0) : 0,
          salesPerson: salesCol ? (row[salesCol] || '').trim() : '',
          deliveryOrderNo: deliveryCol ? (row[deliveryCol] || '').trim() : '',
          distributionDate: dateCol ? (row[dateCol] || '').trim() : dayjs().format('YYYY-MM-DD'),
        });
      }
        if (dtos.length === 0) { message.warning(`无有效数据，跳过 ${skipped} 条`); return; }
        await apiClient.post('/distribution/downstream/batch', dtos);
        message.success(`导入完成: ${dtos.length} 条` + (skipped > 0 ? `, 跳过 ${skipped} 条` : '')); load();
      } catch (ex: any) {
        message.destroy();
        message.error('Excel 解析失败: ' + (ex?.message || '未知错误'));
      }
    };
    reader.onerror = () => {
      message.destroy();
      message.error('文件读取失败，请检查文件是否损坏');
    };
    reader.readAsArrayBuffer(file);
    return false;
  };

  // 采购经销商搜索过滤（按钮触发）
  const filtered = useMemo(() => {
    return data.filter(d => {
      if (sqCustomer && !(d.toCustomerName || '').includes(sqCustomer)) return false;
      return isDateInRange(d.distributionDate, dateRange);
    });
  }, [data, sqCustomer, dateRange]);

  const columns: ColumnsType<DistributionRecordDto> = [
    { title: '药品名称', dataIndex: 'drugName', width: 140,
      sorter: textSorter<DistributionRecordDto>(r => r.drugName),
    },
    { title: '规格型号', dataIndex: 'specification', width: 100,
      sorter: textSorter<DistributionRecordDto>(r => r.specification),
    },
    { title: '批号', dataIndex: 'batchNo', width: 110, sorter: textSorter<DistributionRecordDto>(r => r.batchNo) },
    { title: '数量', dataIndex: 'quantity', width: 70, sorter: numberSorter<DistributionRecordDto>(r => r.quantity) },
    { title: '含税单价', dataIndex: 'taxedUnitPrice', width: 90, sorter: numberSorter<DistributionRecordDto>(r => r.taxedUnitPrice), render: (v: number) => v ? `¥${v.toFixed(2)}` : '-' },
    { title: '业务员', dataIndex: 'salesPerson', width: 80,
      sorter: textSorter<DistributionRecordDto>(r => r.salesPerson),
    },
    { title: '发货单号', dataIndex: 'deliveryOrderNo', width: 120, sorter: textSorter<DistributionRecordDto>(r => r.deliveryOrderNo) },
    { title: '出货日期', dataIndex: 'distributionDate', width: 110,
      sorter: textSorter<DistributionRecordDto>(r => r.distributionDate),
      render: (v: string) => v ? v.substring(0, 10) : '-',
    },
    { title: '采购经销商', dataIndex: 'toCustomerName', width: 180, sorter: textSorter<DistributionRecordDto>(r => r.toCustomerName) },
    { title: '操作', width: 64, render: (_, r) => (
        <RowActions actions={[
          { key: 'delete', label: '删除', icon: <DeleteOutlined />, tone: 'danger', confirmTitle: '确定删除？', onClick: () => handleDelete(r.id) },
        ]} />),
    },
  ];
  const filterFields: TableFilterField<DistributionRecordDto>[] = [
    { key: 'drugName', label: '药品名称', getValue: r => r.drugName },
    { key: 'specification', label: '规格型号', getValue: r => r.specification },
    { key: 'batchNo', label: '批号', getValue: r => r.batchNo },
    { key: 'salesPerson', label: '业务员', getValue: r => r.salesPerson },
    { key: 'toCustomerName', label: '采购经销商', getValue: r => r.toCustomerName },
  ];
  const tableData = applyTableFilters(filtered, filterFields, tableFilters);
  const drugOptions = drugs.map(d => {
    const [name, specPart] = d.name.split(' [');
    const spec = specPart?.replace(']', '') || '';
    return {
      label: spec ? `${name} / ${spec}` : name,
      value: d.id,
      searchText: `${name}${spec}`,
    };
  });

  return (
    <div>
      <Space wrap style={{ marginBottom: 16 }}>
        <Input placeholder="搜索采购经销商" value={searchCustomer} onChange={e => setSearchCustomer(e.target.value)}
          onPressEnter={() => setSqCustomer(searchCustomer)} prefix={<SearchOutlined />} style={{ width: 200 }} allowClear />
        <Button type="primary" icon={<SearchOutlined />} onClick={() => setSqCustomer(searchCustomer)}>搜索</Button>
        <DatePicker.RangePicker
          allowClear
          placeholder={['开始日期', '结束日期']}
          onChange={(_, values) => setDateRange(values[0] && values[1] ? [values[0], values[1]] : null)}
          style={{ width: 250 }}
        />
        <TableFilters rows={filtered} fields={filterFields} value={tableFilters} onChange={setTableFilters} />
        {canWrite && <Button type="primary" icon={<PlusOutlined />} onClick={handleAdd}>录入出货</Button>}
        {canWrite && <Upload accept=".xlsx" showUploadList={false} beforeUpload={handleExcelImport}>
          <Button icon={<UploadOutlined />}>导入Excel</Button>
        </Upload>}
        {canWrite && <Button danger icon={<DeleteOutlined />} onClick={async () => {
          if (window.confirm('确定清空所有记录？')) { await apiClient.delete('/distribution/clear?fromSelf=true'); message.success('已清空'); load(); }
        }}>清空</Button>}
      </Space>
      <Table rowKey="id" columns={columns} dataSource={tableData} loading={loading}
        pagination={{ defaultPageSize: 10, showTotal: () => `共 ${tableData.length} 条`, showSizeChanger: true }} />
      <Modal title="录入出货（本司 → 一级经销商）" open={modalOpen} onOk={handleOk} onCancel={() => setModalOpen(false)} width={560}>
        <Form form={form} layout="vertical">
          <Form.Item name="drugId" label="药品名称 / 规格型号" rules={[{ required: true }]}>
            <Select
              showSearch
              placeholder="选择药品及规格"
              filterOption={(v, o) => String(o?.searchText || o?.label || '').includes(v)}
              options={drugOptions}
            />
          </Form.Item>
          <Form.Item name="batchNo" label="批号" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="customerId" label="采购经销商（一级经销商）" rules={[{ required: true }]}>
            <Select showSearch placeholder="选择一级经销商" filterOption={(v, o) => (o?.label as string || '').includes(v)}
              options={customers.map(c => ({ label: c.name, value: c.id }))} /></Form.Item>
          <Form.Item name="quantity" label="数量（负数表示退货）" rules={[{ required: true }]}><Input placeholder="如 10 或 -5" /></Form.Item>
          <Form.Item name="taxedUnitPrice" label="含税单价"><Input placeholder="0.00" /></Form.Item>
          <Form.Item name="salesPerson" label="业务员"><Input /></Form.Item>
          <Form.Item name="deliveryOrderNo" label="发货单号"><Input /></Form.Item>
          <Form.Item name="distributionDate" label="出货日期">
            <DatePicker style={{ width: '100%' }} placeholder="选择出货日期" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
