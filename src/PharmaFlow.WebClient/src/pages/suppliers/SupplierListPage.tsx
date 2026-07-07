import { useEffect, useState, useCallback } from 'react';
import { Table, Button, Space, Input, Modal, Form, message } from 'antd';
import { DeleteOutlined, EditOutlined, PlusOutlined, SearchOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { getSuppliers, createSupplier, updateSupplier, deleteSupplier } from '../../api/suppliers';
import RowActions from '../../components/RowActions';
import type { SupplierDto } from '../../types';

export default function SupplierListPage() {
  const [data, setData] = useState<SupplierDto[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(20);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<SupplierDto | null>(null);
  const [form] = Form.useForm();

  const load = useCallback(async (p = page, sz = pageSize, s = search) => {
    setLoading(true);
    try { const res = await getSuppliers({ page: p, size: sz, search: s || undefined }); setData(res.items); setTotal(res.totalCount); }
    finally { setLoading(false); }
  }, [page, pageSize, search]);

  useEffect(() => { load(); }, []);

  const handleSearch = () => { setPage(0); load(0, pageSize, search); };
  const handleAdd = () => { setEditing(null); form.resetFields(); setModalOpen(true); };
  const handleEdit = (r: SupplierDto) => { setEditing(r); form.setFieldsValue(r); setModalOpen(true); };
  const handleDelete = async (id: string) => { await deleteSupplier(id); message.success('已删除'); load(); };
  const handleOk = async () => {
    const values = await form.validateFields();
    if (editing) { await updateSupplier(editing.id, values); message.success('更新成功'); }
    else { await createSupplier(values); message.success('创建成功'); }
    setModalOpen(false); load();
  };

  const columns: ColumnsType<SupplierDto> = [
    { title: '编码', dataIndex: 'code', width: 120 }, { title: '名称', dataIndex: 'name', width: 200 },
    { title: '联系人', dataIndex: 'contactPerson', width: 90 }, { title: '电话', dataIndex: 'phone', width: 130 },
    { title: '地址', dataIndex: 'address', width: 200 },
    { title: '操作', width: 110, render: (_, r) => (
        <RowActions actions={[
          { key: 'edit', label: '编辑', icon: <EditOutlined />, tone: 'primary', onClick: () => handleEdit(r) },
          { key: 'delete', label: '删除', icon: <DeleteOutlined />, tone: 'danger', confirmTitle: '确定删除？', onClick: () => handleDelete(r.id) },
        ]} />),
    },
  ];

  return (
    <div>
      <Space wrap style={{ marginBottom: 16 }}>
        <Input placeholder="供应商名称" value={search} onChange={e => setSearch(e.target.value)}
          onPressEnter={handleSearch} prefix={<SearchOutlined />} style={{ width: 200 }} allowClear />
        <Button type="primary" onClick={handleSearch}>搜索</Button>
        <Button type="primary" icon={<PlusOutlined />} onClick={handleAdd}>新增供应商</Button>
      </Space>
      <Table rowKey="id" columns={columns} dataSource={data} loading={loading}
        pagination={{ current: page + 1, total, pageSize, showTotal: t => `共 ${t} 条`, showSizeChanger: true,
          onChange: (p, sz) => { setPage(p - 1); setPageSize(sz); load(p - 1, sz); } }} />
      <Modal title={editing ? '编辑供应商' : '新增供应商'} open={modalOpen} onOk={handleOk} onCancel={() => setModalOpen(false)}>
        <Form form={form} layout="vertical">
          <Form.Item name="name" label="供应商名称" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="code" label="编码" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="contactPerson" label="联系人"><Input /></Form.Item>
          <Form.Item name="phone" label="电话"><Input /></Form.Item>
          <Form.Item name="address" label="地址"><Input /></Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
