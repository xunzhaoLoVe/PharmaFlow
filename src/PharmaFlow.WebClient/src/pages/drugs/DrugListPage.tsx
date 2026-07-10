import { useEffect, useState, useCallback } from 'react';
import { Table, Button, Space, Input, Modal, Form, message, Upload } from 'antd';
import { PlusOutlined, SearchOutlined, UploadOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import * as XLSX from 'xlsx';
import { useAuthStore } from '../../store/authStore';
import { getDrugs, createDrug } from '../../api/drugs';
import TableFilters from '../../components/TableFilters';
import { applyTableFilters, type TableFilterField, type TableFilterValues } from '../../utils/tableFilters';
import { textSorter } from '../../utils/tableSorters';
import type { DrugDto } from '../../types';

export default function DrugListPage() {
  const canWrite = useAuthStore(s => s.canWrite);
  const [data, setData] = useState<DrugDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [tableFilters, setTableFilters] = useState<TableFilterValues>({});
  const [form] = Form.useForm();

  const load = useCallback(async (s = search) => {
    setLoading(true);
    try {
      const res = await getDrugs({ page: 0, size: 99999, search: s || undefined });
      setData(res.items);
    } finally { setLoading(false); }
  }, [search]);

  useEffect(() => { load(); }, []);

  const handleSearch = () => { load(search); };
  const handleAdd = () => { form.resetFields(); setModalOpen(true); };
  const handleOk = async () => {
    const values = await form.validateFields();
    await createDrug(values);
    message.success('创建成功');
    setModalOpen(false); load();
  };

  // Excel 导入
  const handleExcelImport = (file: File) => {
    const hideLoading = message.loading('正在解析 Excel...', 0);
    const reader = new FileReader();
    reader.onload = async (e) => {
      try {
        const wb = XLSX.read(e.target?.result, { type: 'array' });
        const sheet = wb.Sheets[wb.SheetNames[0]];
        const rows = XLSX.utils.sheet_to_json<Record<string, string>>(sheet, { defval: '' });
        if (rows.length === 0) { message.warning('Excel 无数据'); return; }
        const aliases: Record<string, string[]> = {
          code: ['编码', '药品编码', '代码', 'code'],
          name: ['名称', '药品名称', '药品名', 'name', '产品名称', '产品'],
          specification: ['规格', '包装规格', 'specification'],
        };
        let imported = 0, skipped = 0, errors: string[] = [];
        const headers = Object.keys(rows[0]);
        const getVal = (row: Record<string, string>, field: string) => { for (const a of aliases[field] || []) { for (const h of headers) { if (h.trim() === a) return (row[h] || '').trim(); } } return ''; };
        for (const row of rows) {
          const name = getVal(row, 'name'); if (!name) { skipped++; continue; }
          try { await createDrug({ code: getVal(row, 'code'), name, specification: getVal(row, 'specification') }); imported++; } catch (e: any) { skipped++; errors.push(name + ': ' + (e?.message || '未知错误')); }
        }
        if (imported === 0 && skipped > 0 && errors.length > 0) {
          message.error(`导入失败: ${errors.slice(0, 3).join('; ')}`);
        } else {
          message.success(`导入完成: 成功 ${imported} 条, 跳过 ${skipped} 条` + (errors.length > 0 ? ` (前3条错误: ${errors.slice(0, 3).join('; ')})` : ''));
        }
        load();
      } catch (ex: any) {
        message.error('Excel 解析失败: ' + (ex?.message || '未知错误'));
      } finally {
        hideLoading();
      }
    };
    reader.onerror = () => {
      hideLoading();
      message.error('文件读取失败，请检查文件是否损坏');
    };
    reader.readAsArrayBuffer(file);
    return false;
  };

  const columns: ColumnsType<DrugDto> = [
    { title: '药品编码', dataIndex: 'code', width: 150, sorter: textSorter<DrugDto>(r => r.code) },
    { title: '药品名称', dataIndex: 'name', width: 250,
      sorter: textSorter<DrugDto>(r => r.name),
    },
    { title: '规格', dataIndex: 'specification', width: 200,
      sorter: textSorter<DrugDto>(r => r.specification),
    },
  ];
  const filterFields: TableFilterField<DrugDto>[] = [
    { key: 'code', label: '药品编码', getValue: r => r.code },
    { key: 'name', label: '药品名称', getValue: r => r.name },
    { key: 'specification', label: '规格', getValue: r => r.specification },
  ];
  const tableData = applyTableFilters(data, filterFields, tableFilters);

  return (
    <div>
      <Space wrap style={{ marginBottom: 16 }}>
        <Input placeholder="药品名称" value={search} onChange={e => setSearch(e.target.value)}
          onPressEnter={handleSearch} prefix={<SearchOutlined />} style={{ width: 220 }} allowClear />
        <Button type="primary" onClick={handleSearch}>搜索</Button>
        <TableFilters rows={data} fields={filterFields} value={tableFilters} onChange={setTableFilters} />
        {canWrite && <Button type="primary" icon={<PlusOutlined />} onClick={handleAdd}>新增药品</Button>}
        {canWrite && <Upload accept=".xlsx" showUploadList={false} beforeUpload={handleExcelImport}>
          <Button icon={<UploadOutlined />}>导入Excel</Button>
        </Upload>}
      </Space>
      <Table rowKey="id" columns={columns} dataSource={tableData} loading={loading}
        pagination={{ defaultPageSize: 10, showTotal: () => `共 ${tableData.length} 条`, showSizeChanger: true }} />
      <Modal title="新增药品" open={modalOpen} onOk={handleOk} onCancel={() => setModalOpen(false)}>
        <Form form={form} layout="vertical">
          <Form.Item name="name" label="药品名称" rules={[{ required: true, message: '请输入药品名称' }]}><Input /></Form.Item>
          <Form.Item name="code" label="药品编码"><Input placeholder="留空自动生成" /></Form.Item>
          <Form.Item name="specification" label="规格"><Input /></Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
