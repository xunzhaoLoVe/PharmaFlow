import { useState } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { Menu, Button, Dropdown, Modal, Form, Input, message } from 'antd';
import {
  DashboardOutlined, MedicineBoxOutlined, TeamOutlined,
  ShoppingCartOutlined, DollarOutlined,
  InboxOutlined, ApartmentOutlined, AlertOutlined,
  BarChartOutlined, LogoutOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
  UserOutlined, KeyOutlined, CloudDownloadOutlined, AuditOutlined, SettingOutlined,
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
  { key: '/datacollection', icon: <CloudDownloadOutlined />, label: '数据采集', perm: 'datacollection' },
  { key: '/datacollection/review', icon: <AuditOutlined />, label: '数据审核', perm: 'datacollection' },
  { key: '/importrules', icon: <SettingOutlined />, label: '导入规则', perm: 'datacollection' },
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
  const menuItems = filterMenu(user?.permissions || []);
  const selectedKey = menuItems
    .map(item => item.key)
    .filter(key => location.pathname === key || location.pathname.startsWith(`${key}/`))
    .sort((a, b) => b.length - a.length)[0] || location.pathname;
  const currentMenu = menuItems.find(item => item.key === selectedKey);

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
    <div className="app-shell">
      {/* 侧边栏 */}
      <aside className={`app-sider ${collapsed ? 'is-collapsed' : ''}`}>
        <div className="app-brand">
          <span className="app-brand-mark">药</span>
          <span className="app-brand-text">
            药品流向管理
          </span>
        </div>
        <Menu theme="dark" mode="inline" selectedKeys={[selectedKey]}
          items={menuItems}
          onClick={({ key }) => navigate(key)} className="app-menu" inlineCollapsed={collapsed} />
        <div className="app-sider-footer">
          <Button type="text" block onClick={() => setCollapsed(!collapsed)}
            icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            className="app-collapse-button">{collapsed ? '' : '收起'}</Button>
        </div>
      </aside>

      {/* 右侧内容 */}
      <main className="app-main">
        <header className="app-topbar">
          <div className="app-page-heading">
            <span className="app-page-title">{currentMenu?.label || '工作台'}</span>
            <span className="app-page-path">{location.pathname}</span>
          </div>
          <Dropdown menu={{ items: userMenuItems }} placement="bottomRight">
            <Button type="text" icon={<UserOutlined />} className="app-user-button">
              {user?.displayName || '用户'}
            </Button>
          </Dropdown>
        </header>
        <section className="app-content">
          <Outlet />
        </section>
      </main>

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
