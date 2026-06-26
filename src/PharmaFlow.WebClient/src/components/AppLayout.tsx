import { useState } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { Menu, Button, Dropdown, Modal, Form, Input, message } from 'antd';
import {
  DashboardOutlined, MedicineBoxOutlined, TeamOutlined,
  ShoppingCartOutlined, DollarOutlined,
  InboxOutlined, ApartmentOutlined, AlertOutlined,
  BarChartOutlined, LogoutOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
  UserOutlined, KeyOutlined,
} from '@ant-design/icons';
import { useAuthStore } from '../store/authStore';
import { changePassword } from '../api/auth';

const ALL_MENU_ITEMS = [
  { key: '/dashboard', icon: <DashboardOutlined />, label: '首页仪表盘', perm: 'dashboard' },
  { key: '/drugs', icon: <MedicineBoxOutlined />, label: '药品管理', perm: 'drugs' },
  { key: '/customers', icon: <TeamOutlined />, label: '客户管理', perm: 'customers' },
  { key: '/purchases', icon: <ShoppingCartOutlined />, label: '采购管理', perm: 'purchases' },
  { key: '/sales', icon: <DollarOutlined />, label: '销售管理', perm: 'sales' },
  { key: '/inventory', icon: <InboxOutlined />, label: '库存管理', perm: 'inventory' },
  { key: '/distribution', icon: <ApartmentOutlined />, label: '流向查询', perm: 'distribution' },
  { key: '/distribution/conflicts', icon: <AlertOutlined />, label: '串货预警', perm: 'distribution' },
  { key: '/reports', icon: <BarChartOutlined />, label: '报表统计', perm: 'reports' },
  { key: '/users', icon: <UserOutlined />, label: '用户管理', perm: 'users' },
];

function filterMenu(permissions: string[]) {
  if (permissions.includes('*')) return ALL_MENU_ITEMS;
  return ALL_MENU_ITEMS.filter(m => !m.perm || permissions.includes(m.perm));
}

export default function AppLayout() {
  const [collapsed, setCollapsed] = useState(false);
  const [pwdModalOpen, setPwdModalOpen] = useState(false);
  const [pwdLoading, setPwdLoading] = useState(false);
  const [pwdForm] = Form.useForm();
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuthStore();
  const siderW = collapsed ? 80 : 230;

  const handleLogout = () => { logout(); navigate('/login'); };

  const handleChangePwd = async () => {
    try {
      const values = await pwdForm.validateFields();
      setPwdLoading(true);
      await changePassword(values.oldPassword, values.newPassword);
      message.success('密码修改成功，请重新登录');
      setPwdModalOpen(false);
      pwdForm.resetFields();
      setTimeout(() => handleLogout(), 1000);
    } catch (e: any) {
      if (e?.response?.data?.message) message.error(e.response.data.message);
      else if (e?.message && !e.errorFields) message.error(e.message);
    } finally {
      setPwdLoading(false);
    }
  };

  const userMenuItems = [
    { key: 'info', icon: <UserOutlined />, label: `${user?.displayName || ''} (${user?.role || ''})`, disabled: true },
    { type: 'divider' as const },
    { key: 'changePwd', icon: <KeyOutlined />, label: '修改密码', onClick: () => { setPwdModalOpen(true); } },
    { key: 'logout', icon: <LogoutOutlined />, label: '退出登录', onClick: handleLogout },
  ];

  return (
    <div style={{ display: 'flex' }}>
      {/* 侧边栏 */}
      <div style={{ width: siderW, minWidth: siderW, background: '#001529', transition: 'width 0.2s', display: 'flex', flexDirection: 'column', position: 'sticky', top: 0, height: '100vh', overflow: 'auto' }}>
        <div style={{ height: 64, display: 'flex', alignItems: 'center', justifyContent: 'center', borderBottom: '1px solid rgba(255,255,255,0.1)', flexShrink: 0 }}>
          <span style={{ color: '#fff', fontSize: collapsed ? 16 : 18, fontWeight: 'bold', whiteSpace: 'nowrap', overflow: 'hidden' }}>
            {collapsed ? '💊' : '💊 药品流向管理'}
          </span>
        </div>
        <Menu theme="dark" mode="inline" selectedKeys={[location.pathname]}
          items={filterMenu(user?.permissions || [])}
          onClick={({ key }) => navigate(key)} style={{ flex: 1 }} inlineCollapsed={collapsed} />
        <div style={{ padding: '12px 16px', borderTop: '1px solid rgba(255,255,255,0.1)', flexShrink: 0 }}>
          <Button type="text" block onClick={() => setCollapsed(!collapsed)}
            icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            style={{ color: '#fff' }}>{collapsed ? '' : '收起'}</Button>
        </div>
      </div>

      {/* 右侧内容 */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: '#f5f5f5' }}>
        <div style={{ height: 48, background: '#fff', borderBottom: '1px solid #e8e8e8', display: 'flex', alignItems: 'center', justifyContent: 'flex-end', padding: '0 24px' }}>
          <Dropdown menu={{ items: userMenuItems }} placement="bottomRight">
            <Button type="text" icon={<UserOutlined />} style={{ cursor: 'pointer' }}>
              {user?.displayName || '用户'}
            </Button>
          </Dropdown>
        </div>
        <div style={{ flex: 1, padding: 16 }}>
          <div style={{ background: '#fff', borderRadius: 8, padding: 24, minHeight: 280 }}>
            <Outlet />
          </div>
        </div>
      </div>

      {/* 修改密码弹窗 */}
      <Modal title="修改密码" open={pwdModalOpen} onOk={handleChangePwd} onCancel={() => { setPwdModalOpen(false); pwdForm.resetFields(); }} confirmLoading={pwdLoading}>
        <Form form={pwdForm} layout="vertical">
          <Form.Item name="oldPassword" label="原密码" rules={[{ required: true, message: '请输入原密码' }]}>
            <Input.Password />
          </Form.Item>
          <Form.Item name="newPassword" label="新密码" rules={[{ required: true, min: 6, message: '至少6位' }]}>
            <Input.Password />
          </Form.Item>
          <Form.Item name="confirmPwd" label="确认新密码" dependencies={['newPassword']}
            rules={[{ required: true }, ({ getFieldValue }) => ({
              validator(_, value) {
                if (!value || getFieldValue('newPassword') === value) return Promise.resolve();
                return Promise.reject(new Error('两次密码不一致'));
              },
            })]}>
            <Input.Password />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
