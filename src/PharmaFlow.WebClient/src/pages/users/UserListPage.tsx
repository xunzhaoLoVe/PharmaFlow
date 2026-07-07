import { useEffect, useState } from 'react';
import { Table, Button, Modal, Form, Input, Select, message, Tag } from 'antd';
import { CheckCircleOutlined, DeleteOutlined, EditOutlined, KeyOutlined, PlusOutlined, StopOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import apiClient from '../../api/client';
import RowActions from '../../components/RowActions';
import type { ApiResponse } from '../../types';

interface UserDto {
  id: string; username: string; displayName: string;
  phone?: string; email?: string; isActive: boolean;
  role?: string; lastLoginTime?: string; roleId?: string;
}

interface RoleDto {
  id: string; name: string; description?: string; permissions: string[];
}

export default function UserListPage() {
  const [data, setData] = useState<UserDto[]>([]);
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<UserDto | null>(null);
  const [form] = Form.useForm();
  const [editForm] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try {
      const [usersRes, rolesRes] = await Promise.all([
        apiClient.get<ApiResponse<UserDto[]>>('/auth/users'),
        apiClient.get<ApiResponse<RoleDto[]>>('/auth/roles'),
      ]);
      setData((usersRes as any).data || []);
      setRoles((rolesRes as any).data || []);
    } finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []);

  const handleCreate = async () => {
    const values = await form.validateFields();
    try {
      await apiClient.post('/auth/users', values);
      message.success('用户创建成功');
      setModalOpen(false); form.resetFields(); load();
    } catch (e: any) {
      message.error(e?.response?.data?.message || '创建失败');
    }
  };

  const handleEdit = async () => {
    const values = await editForm.validateFields();
    try {
      await apiClient.put(`/auth/users/${editingUser!.id}`, values);
      message.success('更新成功');
      setEditModalOpen(false); load();
    } catch (e: any) {
      message.error(e?.response?.data?.message || '更新失败');
    }
  };

  const handleDelete = async (id: string) => {
    await apiClient.delete(`/auth/users/${id}`);
    message.success('已删除');
    load();
  };

  const toggleActive = async (id: string) => {
    await apiClient.put(`/auth/users/${id}/toggle-active`);
    load();
  };

  const resetPassword = async (id: string) => {
    const pwd = prompt('请输入新密码（至少6位）:');
    if (!pwd || pwd.length < 6) { message.warning('密码至少6位'); return; }
    await apiClient.put(`/auth/users/${id}/reset-password?newPassword=${encodeURIComponent(pwd)}`);
    message.success('密码已重置');
  };

  const columns: ColumnsType<UserDto> = [
    { title: '用户名', dataIndex: 'username', width: 110 },
    { title: '显示名', dataIndex: 'displayName', width: 100 },
    { title: '角色', dataIndex: 'role', width: 100,
      render: (v: string) => {
        const colors: Record<string, string> = { '管理员': 'red', '操作员': 'blue', '只读用户': 'green' };
        return <Tag color={colors[v] || 'default'}>{v || '-'}</Tag>;
      },
    },
    { title: '状态', dataIndex: 'isActive', width: 70,
      render: (v: boolean) => <Tag color={v ? 'green' : 'red'}>{v ? '启用' : '禁用'}</Tag>,
    },
    { title: '最近登录', dataIndex: 'lastLoginTime', width: 155,
      render: (v: string) => v ? new Date(v).toLocaleString() : '-',
    },
    { title: '操作', width: 210, render: (_, r) => (
      <RowActions actions={[
        { key: 'edit', label: '编辑', icon: <EditOutlined />, tone: 'primary', onClick: () => {
          setEditingUser(r);
          editForm.setFieldsValue({
            displayName: r.displayName,
            phone: r.phone,
            email: r.email,
            roleId: r.roleId || undefined,
            isActive: r.isActive,
          });
          setEditModalOpen(true);
        } },
        { key: 'toggle', label: r.isActive ? '禁用' : '启用', icon: r.isActive ? <StopOutlined /> : <CheckCircleOutlined />, onClick: () => toggleActive(r.id) },
        { key: 'reset', label: '重置密码', icon: <KeyOutlined />, confirmTitle: '确定重置密码？', onClick: () => resetPassword(r.id) },
        { key: 'delete', label: '删除', icon: <DeleteOutlined />, tone: 'danger', confirmTitle: '确定删除该用户？', onClick: () => handleDelete(r.id) },
      ]} />
    )},
  ];

  return (
    <div>
      <Button type="primary" icon={<PlusOutlined />} onClick={() => { form.resetFields(); setModalOpen(true); }}
        style={{ marginBottom: 16 }}>新增用户</Button>
      <Table rowKey="id" columns={columns} dataSource={data} loading={loading}
        pagination={{ defaultPageSize: 10, showTotal: (t) => `共 ${t} 条` }} />

      {/* 新增弹窗 */}
      <Modal title="新增用户" open={modalOpen} onOk={handleCreate} onCancel={() => setModalOpen(false)}>
        <Form form={form} layout="vertical">
          <Form.Item name="username" label="用户名" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="displayName" label="显示名" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="password" label="密码" rules={[{ required: true, min: 6 }]}>
            <Input.Password />
          </Form.Item>
          <Form.Item name="roleId" label="角色">
            <Select placeholder="选择角色" allowClear
              options={roles.map(r => ({ label: `${r.name} - ${r.description || ''}`, value: r.id }))} />
          </Form.Item>
          <Form.Item name="phone" label="手机"><Input /></Form.Item>
          <Form.Item name="email" label="邮箱"><Input /></Form.Item>
        </Form>
      </Modal>

      {/* 编辑弹窗 */}
      <Modal title="编辑用户" open={editModalOpen} onOk={handleEdit} onCancel={() => setEditModalOpen(false)}>
        <Form form={editForm} layout="vertical">
          <Form.Item name="displayName" label="显示名"><Input /></Form.Item>
          <Form.Item name="roleId" label="角色">
            <Select placeholder="选择角色" allowClear
              options={roles.map(r => ({ label: `${r.name} - ${r.description || ''}`, value: r.id }))} />
          </Form.Item>
          <Form.Item name="isActive" label="状态">
            <Select options={[{ label: '启用', value: true }, { label: '禁用', value: false }]} />
          </Form.Item>
          <Form.Item name="phone" label="手机"><Input /></Form.Item>
          <Form.Item name="email" label="邮箱"><Input /></Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
