import { useEffect, useState, useCallback } from 'react';
import { Table, Button, Space, Input, Modal, Form, Select, message, Popconfirm, Upload } from 'antd';
import { PlusOutlined, UploadOutlined, DeleteOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import * as XLSX from 'xlsx';
import apiClient from '../../api/client';
import { getDrugDropdown } from '../../api/drugs';
import { getCustomerDropdown } from '../../api/customers';
import { getDistribution, recordDownstream, deleteDistribution } from '../../api/distribution';
import type { ApiResponse, DistributionRecordDto, DropdownItem } from '../../types';

export default function SalesListPage() {
  const [data, setData] = useState<DistributionRecordDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchFrom, setSearchFrom] = useState('');
  const [searchTo, setSearchTo] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [drugs, setDrugs] = useState<DropdownItem[]>([]);
  const [fromCustomers, setFromCustomers] = useState<DropdownItem[]>([]);
  const [toCustomers, setToCustomers] = useState<DropdownItem[]>([]);
  const [drugSpecMap, setDrugSpecMap] = useState<Record<string, string>>({});
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
    const fc = await getCustomerDropdown();
    const tc = await apiClient.get<ApiResponse<DropdownItem[]>>('/customers/end-dropdown').then(r => r.data!);
    setDrugs(d); setFromCustomers(fc); setToCustomers([...fc, ...tc].sort((a, b) => a.name.localeCompare(b.name)));
    const map: Record<string, string> = {};
    d.forEach(drug => { map[drug.id] = drug.name.split(' [')[1]?.replace(']', '') || ''; });
    setDrugSpecMap(map);
  };

  // 客户端筛选：来源、去向
  const filtered = data.filter(item => {
    if (searchFrom && !(item.fromCustomerName || '').includes(searchFrom)) return false;
    if (searchTo && !(item.toCustomerName || '').includes(searchTo)) return false;
    return true;
  });

  const handleAdd = () => {
    form.resetFields();
    form.setFieldsValue({ quantity: 1 });
    setModalOpen(true);
  };
  const handleDelete = async (id: string) => { await deleteDistribution(id); message.success('已删除'); load(); };

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
      distributionDate: values.distributionDate || '',
      province: values.province || '',
    });
    message.success('下游流转记录已保存'); setModalOpen(false); load();
  };

  // Excel 导入
  const handleExcelImport = async (file: File) => {
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

        const [allDrugs, allFrom, allTo] = await Promise.all([
          getDrugDropdown(), getCustomerDropdown(),
          apiClient.get<ApiResponse<DropdownItem[]>>('/customers/end-dropdown').then(r => r.data!),
        ]);
        const allCustomers = [...allFrom, ...allTo];
        const v = (r: any, c: string | null) => c ? String(r[c] || '').trim() : '';
        const dtos = []; let skipped = 0;

        for (const row of rows) {
          const drugVal = v(row, drugCol); const specVal = v(row, specCol);
          const fromVal = v(row, fromCol); const toVal = v(row, toCol);
          if (!drugVal || !fromVal || !toVal) { skipped++; continue; }
          const drug = allDrugs.find((d: any) => (d.name || '').includes(drugVal) && (!specVal || (d.name || '').includes(specVal)));
          const fromC = allFrom.find((c: any) => c.name.includes(fromVal));
          const toC = allCustomers.find((c: any) => c.name.includes(toVal));
          if (!drug || !fromC || !toC) { skipped++; continue; }
          dtos.push({
            drugId: drug.id, drugName: (drug.name || '').split(' [')[0],
            batchNo: v(row, batchCol) || '',
            fromCustomerId: fromC.id, fromCustomerName: fromC.name,
            toCustomerId: toC.id, toCustomerName: toC.name,
            quantity: parseFloat(v(row, qtyCol)) || 1,
            distributionDate: v(row, dateCol).replace(/^(\d{4}-\d{2})$/, '$1-01'),
            distributionDateDisplay: v(row, dateCol),
            taxedUnitPrice: 0,
            province: v(row, provCol),
          });
        }
        if (dtos.length === 0) { message.warning(`无有效数据，跳过 ${skipped} 条`); return; }
        const BATCH = 500;
        for (let i = 0; i < dtos.length; i += BATCH) {
          await apiClient.post('/distribution/downstream/batch', dtos.slice(i, i + BATCH));
        }
        message.success(`导入完成: ${dtos.length} 条` + (skipped > 0 ? `, 跳过 ${skipped} 条` : '')); load();
      } catch (err: any) { message.error('导入失败: ' + (err?.message || '未知错误')); }
    };
    reader.onerror = () => message.error('文件读取失败');
    reader.readAsArrayBuffer(file);
    return false;
  };

  const columns: ColumnsType<DistributionRecordDto> = [
    { title: '药品名称', dataIndex: 'drugName', width: 130,
      filters: [...new Set((filtered.length > 0 ? filtered : data).map(d => d.drugName).filter(Boolean))].sort().map(n => ({ text: n, value: n })),
      onFilter: (v, r) => r.drugName === (v as string),
    },
    { title: '规格', dataIndex: 'specification', width: 100,
      filters: [...new Set((filtered.length > 0 ? filtered : data).map(d => d.specification).filter(Boolean))].sort().map(s => ({ text: s, value: s })),
      onFilter: (v, r) => (r.specification || '') === (v as string),
    },
    { title: '批号', dataIndex: 'batchNo', width: 110 },
    { title: '来源', dataIndex: 'fromCustomerName', width: 160 },
    { title: '去向', dataIndex: 'toCustomerName', width: 160 },
    { title: '数量', dataIndex: 'quantity', width: 70 },
    { title: '省份', dataIndex: 'province', width: 80,
      filters: [...new Set((filtered.length > 0 ? filtered : data).map(d => d.province).filter(Boolean))].sort().map(p => ({ text: p, value: p })),
      onFilter: (v, r) => (r.province || '') === (v as string),
    },
    { title: '日期', dataIndex: 'distributionDateDisplay', width: 100,
      render: (v: string, r: DistributionRecordDto) => v || (r.distributionDate ? r.distributionDate.substring(0, 10) : '-'),
    },
    { title: '操作', width: 80, render: (_, r) => (
        <Popconfirm title="确定删除？" onConfirm={() => handleDelete(r.id)}><Button size="small" danger>删除</Button></Popconfirm>),
    },
  ];

  return (
    <div>
      <h2 style={{ marginBottom: 16 }}>📤 销售管理（下游流转）</h2>
      <Space wrap style={{ marginBottom: 16 }}>
        <Input placeholder="来源客户" value={searchFrom} onChange={e => setSearchFrom(e.target.value)}
          style={{ width: 160 }} allowClear />
        <Input placeholder="去向客户" value={searchTo} onChange={e => setSearchTo(e.target.value)}
          style={{ width: 160 }} allowClear />
        <Button type="primary" icon={<PlusOutlined />} onClick={handleAdd}>录入下游流转</Button>
        <Upload accept=".xlsx" showUploadList={false} beforeUpload={handleExcelImport}>
          <Button icon={<UploadOutlined />}>导入Excel</Button>
        </Upload>
        <Button danger icon={<DeleteOutlined />} onClick={async () => {
          if (window.confirm('确定清空销售记录？')) { await apiClient.delete('/distribution/clear?fromSelf=false'); message.success('已清空'); load(); }
        }}>清空</Button>
      </Space>
      <Table rowKey="id" columns={columns} dataSource={filtered} loading={loading}
        pagination={{ defaultPageSize: 10, showTotal: () => `共 ${filtered.length} 条`, showSizeChanger: true }} />
      <Modal title="录入下游流转" open={modalOpen} onOk={handleOk} onCancel={() => setModalOpen(false)} width={560}>
        <Form form={form} layout="vertical">
          <Form.Item name="drugId" label="药品" rules={[{ required: true }]}>
            <Select showSearch placeholder="选择药品"
              filterOption={(v, o) => (o?.label as string || '').includes(v)}
              onChange={id => { form.setFieldsValue({ specification: drugSpecMap[id] || '' }); }}
              options={drugs.map(d => ({ label: d.name.split(' [')[0], value: d.id }))} /></Form.Item>
          <Form.Item name="specification" label="规格"><Input disabled placeholder="自动填充" /></Form.Item>
          <Form.Item name="batchNo" label="批号" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="fromCustomerId" label="来源客户" rules={[{ required: true }]}>
            <Select showSearch placeholder="选择来源"
              filterOption={(v, o) => (o?.label as string || '').includes(v)}
              options={fromCustomers.map(c => ({ label: c.name, value: c.id }))} /></Form.Item>
          <Form.Item name="toCustomerId" label="去向客户" rules={[{ required: true }]}>
            <Select showSearch placeholder="选择去向"
              filterOption={(v, o) => (o?.label as string || '').includes(v)}
              options={toCustomers.map(c => ({ label: c.name, value: c.id }))} /></Form.Item>
          <Form.Item name="province" label="省份"><Input placeholder="如 江苏" /></Form.Item>
          <Form.Item name="quantity" label="数量" rules={[{ required: true }]}><Input placeholder="如 10 或 -5" /></Form.Item>
          <Form.Item name="distributionDate" label="日期"><Input placeholder="2026-05 或 2026-05-06" /></Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
