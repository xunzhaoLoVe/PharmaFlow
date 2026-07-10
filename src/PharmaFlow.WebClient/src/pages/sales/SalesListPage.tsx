import { useEffect, useState, useCallback } from 'react';
import { Table, Button, Space, Input, Modal, Form, Select, message, Upload, DatePicker, Dropdown } from 'antd';
import { PlusOutlined, UploadOutlined, DeleteOutlined, SearchOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import * as XLSX from 'xlsx';
import { useAuthStore } from '../../store/authStore';
import apiClient from '../../api/client';
import { getDrugDropdown } from '../../api/drugs';
import { getDistributorDropdown } from '../../api/customers';
import { getDistribution, recordDownstream, deleteDistribution } from '../../api/distribution';
import RowActions from '../../components/RowActions';
import TableFilters from '../../components/TableFilters';
import { isDateInRange, type DateRangeValue } from '../../utils/dateRange';
import { applyTableFilters, type TableFilterField, type TableFilterValues } from '../../utils/tableFilters';
import { numberSorter, textSorter } from '../../utils/tableSorters';
import type { ApiResponse, DistributionRecordDto, DropdownItem } from '../../types';

export default function SalesListPage() {
  const canWrite = useAuthStore(s => s.canWrite);
  const [data, setData] = useState<DistributionRecordDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchFrom, setSearchFrom] = useState('');
  const [searchTo, setSearchTo] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [drugs, setDrugs] = useState<DropdownItem[]>([]);
  const [fromCustomers, setFromCustomers] = useState<DropdownItem[]>([]);
  const [toCustomers, setToCustomers] = useState<DropdownItem[]>([]);
  const [tableFilters, setTableFilters] = useState<TableFilterValues>({});
  const [dateRange, setDateRange] = useState<DateRangeValue>(null);
  const [form] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getDistribution({ page: 0, size: 99999, fromSelf: false });
      setData(res.items);
    } finally { setLoading(false); }
  }, []);

  useEffect(() => { load(); loadMeta().catch(() => {}); }, []);

  const loadMeta = async () => {
    const d = await getDrugDropdown();
    const fc = await getDistributorDropdown();
    const tc = await apiClient.get<ApiResponse<DropdownItem[]>>('/customers/end-dropdown').then(r => r.data!);
    setDrugs(d); setFromCustomers(fc); setToCustomers([...fc, ...tc].sort((a, b) => a.name.localeCompare(b.name)));
  };

  const [searchFrom2, setSearchFrom2] = useState('');
  const [searchTo2, setSearchTo2] = useState('');

  // 客户端筛选：来源、去向（点击搜索后生效）
  const filtered = data.filter(item => {
    if (searchFrom2 && !(item.fromCustomerName || '').includes(searchFrom2)) return false;
    if (searchTo2 && !(item.toCustomerName || '').includes(searchTo2)) return false;
    if (!isDateInRange(item.distributionDateDisplay || item.distributionDate, dateRange)) return false;
    return true;
  });

  const handleAdd = () => {
    form.resetFields();
    form.setFieldsValue({ quantity: 1, distributionDate: dayjs() });
    setModalOpen(true);
  };
  const handleDelete = async (id: string) => { await deleteDistribution(id); message.success('已删除'); load(); };
  const handleClearSales = async (origin: 'manual' | 'sync') => {
    const label = origin === 'sync' ? '采集同步销售数据' : 'Excel/手工销售数据';
    if (!window.confirm(`确定清空${label}？清空后会同步还原相关库存。`)) return;
    await apiClient.delete('/distribution/clear-sales', { params: { origin } });
    message.success(`已清空${label}`);
    load();
  };

  const handleOk = async () => {
    const values = await form.validateFields();
    const selectedDrug = drugs.find(d => d.id === values.drugId);
    const fromCust = fromCustomers.find(c => c.id === values.fromCustomerId);
    const toCust = toCustomers.find(c => c.id === values.toCustomerId);
    await recordDownstream({
      drugId: values.drugId, drugName: selectedDrug?.name?.split(' [')[0] || '',
      batchNo: values.batchNo,
      fromCustomerId: values.fromCustomerId, fromCustomerName: fromCust?.name || '',
      toCustomerId: values.toCustomerId, toCustomerName: toCust?.name || '',
      quantity: parseFloat(values.quantity) || 0,
      distributionDate: values.distributionDate ? dayjs(values.distributionDate).format('YYYY-MM-DD') : '',
      province: values.province || '',
    });
    message.success('下游流转记录已保存'); setModalOpen(false); load();
  };

  // Excel 导入
  const handleExcelImport = async (file: File) => {
    const hideLoading = message.loading('正在解析 Excel...', 0);
    const reader = new FileReader();
    reader.onload = async (e) => {
      try {
        const wb = XLSX.read(e.target?.result, { type: 'array' });
        const rows = XLSX.utils.sheet_to_json<Record<string, string>>(wb.Sheets[wb.SheetNames[0]], { defval: '' });
        if (rows.length === 0) { message.warning('Excel 无数据'); return; }
        const headers = Object.keys(rows[0]);
        const findCol = (aliases: string[]) => { for (const h of headers) if (aliases.includes(h.trim())) return h; return null; };
        const drugCol = findCol(['药品', '药品名称']); const specCol = findCol(['规格', '规格型号']);
        const batchCol = findCol(['批号', '批次号']); const fromCol = findCol(['来源', '来源客户', '上级经销商', '发货方', '上一级']);
        const toCol = findCol(['去向', '去向客户', '下级经销商', '收货方', '下一级', '终端客户']);
        const qtyCol = findCol(['数量', '流转数量']); const dateCol = findCol(['日期', '流转日期']); const provCol = findCol(['省份', '省']);
        if (!drugCol || !fromCol || !toCol) { message.error('缺少必要列：药品、来源、去向'); return; }

        const v = (r: any, c: string | null) => c ? String(r[c] || '').trim() : '';
        const dtos = []; let skipped = 0;

        for (const row of rows) {
          const drugVal = v(row, drugCol); const specVal = v(row, specCol);
          const fromVal = v(row, fromCol); const toVal = v(row, toCol);
          if (!drugVal || !fromVal || !toVal) { skipped++; continue; }
          dtos.push({
            drugName: drugVal,
            specification: specVal,
            batchNo: v(row, batchCol) || '',
            fromCustomerName: fromVal,
            toCustomerName: toVal,
            quantity: parseFloat(v(row, qtyCol)) || 1,
            distributionDate: v(row, dateCol).replace(/^(\d{4}-\d{2})$/, '$1-01'),
            distributionDateDisplay: v(row, dateCol),
            taxedUnitPrice: 0,
            province: v(row, provCol),
          });
        }
        if (dtos.length === 0) { message.warning(`无有效数据，跳过 ${skipped} 条`); return; }
        const BATCH = 500;
        let imported = 0, duplicateSkipped = 0, supplemented = 0;
        for (let i = 0; i < dtos.length; i += BATCH) {
          const res = await apiClient.post('/distribution/downstream/batch', dtos.slice(i, i + BATCH));
          const result = (res as any).data || { imported: 0, skipped: 0, supplemented: 0 };
          imported += result.imported || 0;
          duplicateSkipped += result.skipped || 0;
          supplemented += result.supplemented || 0;
        }
        message.success(`导入完成: 新增 ${imported} 条，跳过重复 ${duplicateSkipped} 条` + (skipped > 0 ? `，跳过无效 ${skipped} 条` : '') + (supplemented > 0 ? `，补充 ${supplemented} 条` : '')); load();
      } catch (err: any) {
        message.error('导入失败: ' + (err?.message || '未知错误'));
      } finally {
        hideLoading();
      }
    };
    reader.onerror = () => { hideLoading(); message.error('文件读取失败'); };
    reader.readAsArrayBuffer(file);
    return false;
  };

  const columns: ColumnsType<DistributionRecordDto> = [
    { title: '药品名称', dataIndex: 'drugName', width: 130,
      sorter: textSorter<DistributionRecordDto>(r => r.drugName),
    },
    { title: '规格', dataIndex: 'specification', width: 100,
      sorter: textSorter<DistributionRecordDto>(r => r.specification),
    },
    { title: '批号', dataIndex: 'batchNo', width: 110, sorter: textSorter<DistributionRecordDto>(r => r.batchNo) },
    { title: '来源', dataIndex: 'fromCustomerName', width: 160, sorter: textSorter<DistributionRecordDto>(r => r.fromCustomerName) },
    { title: '去向', dataIndex: 'toCustomerName', width: 160, sorter: textSorter<DistributionRecordDto>(r => r.toCustomerName) },
    { title: '数量', dataIndex: 'quantity', width: 70, sorter: numberSorter<DistributionRecordDto>(r => r.quantity) },
    { title: '省份', dataIndex: 'province', width: 80,
      sorter: textSorter<DistributionRecordDto>(r => r.province),
    },
    { title: '日期', dataIndex: 'distributionDateDisplay', width: 100,
      sorter: textSorter<DistributionRecordDto>(r => r.distributionDateDisplay || r.distributionDate),
      render: (v: string, r: DistributionRecordDto) => v || (r.distributionDate ? r.distributionDate.substring(0, 10) : '-'),
    },
    { title: '操作', width: 64, render: (_, r) => (
        <RowActions actions={[
          { key: 'delete', label: '删除', icon: <DeleteOutlined />, tone: 'danger', confirmTitle: '确定删除？', onClick: () => handleDelete(r.id) },
        ]} />),
    },
  ];
  const filterFields: TableFilterField<DistributionRecordDto>[] = [
    { key: 'drugName', label: '药品名称', getValue: r => r.drugName },
    { key: 'specification', label: '规格', getValue: r => r.specification },
    { key: 'batchNo', label: '批号', getValue: r => r.batchNo },
    { key: 'fromCustomerName', label: '来源', getValue: r => r.fromCustomerName },
    { key: 'toCustomerName', label: '去向', getValue: r => r.toCustomerName },
    { key: 'province', label: '省份', getValue: r => r.province },
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
        <Input placeholder="来源客户" value={searchFrom} onChange={e => setSearchFrom(e.target.value)}
          style={{ width: 160 }} allowClear />
        <Input placeholder="去向客户" value={searchTo} onChange={e => setSearchTo(e.target.value)}
          style={{ width: 160 }} allowClear />
        <Button type="primary" icon={<SearchOutlined />} onClick={() => { setSearchFrom2(searchFrom); setSearchTo2(searchTo); }}>搜索</Button>
        <DatePicker.RangePicker
          allowClear
          placeholder={['开始日期', '结束日期']}
          onChange={(_, values) => setDateRange(values[0] && values[1] ? [values[0], values[1]] : null)}
          style={{ width: 250 }}
        />
        <TableFilters rows={filtered} fields={filterFields} value={tableFilters} onChange={setTableFilters} />
        {canWrite && <Button type="primary" icon={<PlusOutlined />} onClick={handleAdd}>录入下游流转</Button>}
        {canWrite && <Upload accept=".xlsx" showUploadList={false} beforeUpload={handleExcelImport}>
          <Button icon={<UploadOutlined />}>导入Excel</Button>
        </Upload>}
        {canWrite && (
          <Dropdown
            trigger={['click']}
            menu={{
              items: [
                { key: 'manual', label: '清空 Excel/手工销售数据', danger: true },
                { key: 'sync', label: '清空采集同步销售数据', danger: true },
              ],
              onClick: ({ key }) => handleClearSales(key as 'manual' | 'sync'),
            }}
          >
            <Button danger icon={<DeleteOutlined />}>清空</Button>
          </Dropdown>
        )}
      </Space>
      <Table rowKey="id" columns={columns} dataSource={tableData} loading={loading}
        pagination={{ defaultPageSize: 10, showTotal: () => `共 ${tableData.length} 条`, showSizeChanger: true }} />
      <Modal title="录入下游流转" open={modalOpen} onOk={handleOk} onCancel={() => setModalOpen(false)} width={560}>
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
          <Form.Item name="fromCustomerId" label="来源经销商" rules={[{ required: true }]}>
            <Select showSearch placeholder="选择来源经销商"
              filterOption={(v, o) => (o?.label as string || '').includes(v)}
              options={fromCustomers.map(c => ({ label: c.name, value: c.id }))} /></Form.Item>
          <Form.Item name="toCustomerId" label="去向客户" rules={[{ required: true }]}>
            <Select showSearch placeholder="选择去向"
              filterOption={(v, o) => (o?.label as string || '').includes(v)}
              options={toCustomers.map(c => ({ label: c.name, value: c.id }))} /></Form.Item>
          <Form.Item name="province" label="省份"><Input placeholder="如 江苏" /></Form.Item>
          <Form.Item name="quantity" label="数量" rules={[{ required: true }]}><Input placeholder="如 10 或 -5" /></Form.Item>
          <Form.Item name="distributionDate" label="日期" rules={[{ required: true, message: '请选择日期' }]}>
            <DatePicker allowClear={false} style={{ width: '100%' }} placeholder="选择日期" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
