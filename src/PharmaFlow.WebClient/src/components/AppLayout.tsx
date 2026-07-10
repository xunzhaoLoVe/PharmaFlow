import { useState } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { Menu, Button, Dropdown, Modal, Form, Input, message } from 'antd';
import {
  DashboardOutlined, MedicineBoxOutlined, TeamOutlined,
  ShoppingCartOutlined, DollarOutlined,
  InboxOutlined, ApartmentOutlined, AlertOutlined,
  BarChartOutlined, LogoutOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
  UserOutlined, KeyOutlined, CloudDownloadOutlined, SettingOutlined,
  DatabaseOutlined, ProfileOutlined, UserSwitchOutlined, BellOutlined,
  CheckOutlined, DownOutlined, MoonOutlined, SunOutlined,
} from '@ant-design/icons';
import { useAuthStore } from '../store/authStore';
import { changePassword } from '../api/auth';

const ALL_MENU_ITEMS = [
  { key: '/dashboard', icon: <DashboardOutlined />, label: '首页仪表盘', perm: 'dashboard' },
  { key: '/drugs', icon: <MedicineBoxOutlined />, label: '药品管理', perm: 'drugs' },
  {
    key: '/customers',
    icon: <TeamOutlined />,
    label: '客户管理',
    perm: 'customers',
    children: [
      { key: '/customers/distributors', icon: <ApartmentOutlined />, label: '经销商', perm: 'customers' },
      { key: '/customers/end', icon: <UserSwitchOutlined />, label: '终端机构', perm: 'customers' },
    ],
  },
  { key: '/purchases', icon: <ShoppingCartOutlined />, label: '采购管理', perm: 'purchases' },
  { key: '/sales', icon: <DollarOutlined />, label: '销售管理', perm: 'sales' },
  { key: '/inventory', icon: <InboxOutlined />, label: '库存管理', perm: 'inventory' },
  { key: '/distribution', icon: <ApartmentOutlined />, label: '流向查询', perm: 'distribution' },
  { key: '/distribution/conflicts', icon: <AlertOutlined />, label: '串货预警', perm: 'distribution' },
  { key: '/reports', icon: <BarChartOutlined />, label: '报表统计', perm: 'reports' },
  {
    key: '/datacollection',
    icon: <CloudDownloadOutlined />,
    label: '数据采集',
    perm: 'datacollection',
    children: [
      { key: '/datacollection/sources', icon: <SettingOutlined />, label: '采集源配置', perm: 'datacollection' },
      { key: '/datacollection/data', icon: <DatabaseOutlined />, label: '采集数据管理', perm: 'datacollection' },
      { key: '/datacollection/summary', icon: <ProfileOutlined />, label: '采集汇总', perm: 'datacollection' },
    ],
  },
  { key: '/users', icon: <UserOutlined />, label: '用户管理', perm: 'users' },
];

const PAGE_DESCRIPTIONS: Record<string, string> = {
  '/dashboard': '客户、药品与库存预警概览',
  '/drugs': '维护药品基础资料与规格信息',
  '/customers/distributors': '经销商客户档案与级别管理',
  '/customers/end': '终端机构档案与类型管理',
  '/purchases': '本司出货至一级经销商记录',
  '/sales': '下游流转销售记录管理',
  '/inventory': '经销商库存与低库存预警',
  '/distribution': '按批号追踪药品流向',
  '/distribution/conflicts': '多规则串货风险预警',
  '/reports': '销售、库存与渠道统计分析',
  '/users': '系统用户与角色权限管理',
  '/datacollection/sources': '采集源配置与任务触发',
  '/datacollection/data': '采集数据查看、映射与提交',
  '/datacollection/summary': '采集汇总数据与同步结果',
};

function filterMenu(permissions: string[]) {
  if (permissions.includes('*')) return ALL_MENU_ITEMS;
  return ALL_MENU_ITEMS
    .map(item => item.children
      ? { ...item, children: item.children.filter(child => !child.perm || permissions.includes(child.perm)) }
      : item)
    .filter(item => (!item.perm || permissions.includes(item.perm)) && (!item.children || item.children.length > 0));
}

function flattenMenu(items: typeof ALL_MENU_ITEMS): { key: string; label: string }[] {
  return items.flatMap(item => [
    { key: item.key, label: item.label },
    ...((item.children ?? []).map(child => ({ key: child.key, label: child.label }))),
  ]);
}

export default function AppLayout() {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [darkMode, setDarkMode] = useState(() => localStorage.getItem('themeMode') === 'dark');
  const [language, setLanguage] = useState(() => localStorage.getItem('language') === 'en' ? 'en' : 'zh');
  const [pwdModalOpen, setPwdModalOpen] = useState(false);
  const [pwdLoading, setPwdLoading] = useState(false);
  const [pwdForm] = Form.useForm();
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuthStore();
  const menuItems = filterMenu(user?.permissions || []);
  const flatMenuItems = flattenMenu(menuItems as typeof ALL_MENU_ITEMS);
  const selectedKey = flatMenuItems
    .map(item => item.key)
    .filter(key => location.pathname === key || (!['/customers', '/datacollection'].includes(key) && location.pathname.startsWith(`${key}/`)))
    .sort((a, b) => b.length - a.length)[0] || location.pathname;
  const currentMenu = flatMenuItems.find(item => item.key === selectedKey);
  const pageDescription = PAGE_DESCRIPTIONS[selectedKey] || '业务数据维护与管理';
  const openKeys = [
    ...(location.pathname.startsWith('/customers') ? ['/customers'] : []),
    ...(location.pathname.startsWith('/datacollection') ? ['/datacollection'] : []),
  ];

  const handleLogout = () => { logout(); navigate('/login'); };
  const handleMenuNavigate = (key: string) => {
    navigate(key);
    setMobileMenuOpen(false);
  };

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
  const languageMenuItems = [
    {
      key: 'en',
      label: (
        <span className="app-language-option">
          <span>US</span>
          <span>English</span>
          {language === 'en' && <CheckOutlined />}
        </span>
      ),
    },
    {
      key: 'zh',
      label: (
        <span className="app-language-option">
          <span>CN</span>
          <span>中文</span>
          {language === 'zh' && <CheckOutlined />}
        </span>
      ),
    },
  ];

  const handleLanguageChange = ({ key }: { key: string }) => {
    const nextLanguage = key === 'en' ? 'en' : 'zh';
    setLanguage(nextLanguage);
    localStorage.setItem('language', nextLanguage);
  };

  const handleThemeMode = () => {
    const nextMode = !darkMode;
    setDarkMode(nextMode);
    localStorage.setItem('themeMode', nextMode ? 'dark' : 'light');
  };

  return (
    <div className={`app-shell ${darkMode ? 'is-dark-mode' : ''}${mobileMenuOpen ? ' is-mobile-menu-open' : ''}`}>
      {/* 侧边栏 */}
      <button
        type="button"
        className="app-mobile-scrim"
        aria-label="关闭菜单"
        onClick={() => setMobileMenuOpen(false)}
      />
      <aside className={`app-sider ${collapsed ? 'is-collapsed' : ''}`}>
        <div className="app-brand">
          <span className="app-brand-mark">药</span>
          <span className="app-brand-text">
            药品流向管理
          </span>
        </div>
        <Menu theme="light" mode="inline" selectedKeys={[selectedKey]} defaultOpenKeys={openKeys}
          items={menuItems}
          onClick={({ key }) => handleMenuNavigate(key)} className="app-menu" inlineCollapsed={collapsed && !mobileMenuOpen} />
        <div className="app-sider-footer">
          <Button type="text" block onClick={handleThemeMode}
            icon={darkMode ? <SunOutlined /> : <MoonOutlined />}
            className="app-sider-action-button">{collapsed && !mobileMenuOpen ? '' : (darkMode ? '浅色模式' : '深色模式')}</Button>
          <Button type="text" block onClick={() => setCollapsed(!collapsed)}
            icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            className="app-sider-action-button app-collapse-button">{collapsed && !mobileMenuOpen ? '' : '收起'}</Button>
        </div>
      </aside>

      {/* 右侧内容 */}
      <main className="app-main">
        <header className="app-topbar">
          <Button
            type="text"
            icon={<MenuUnfoldOutlined />}
            className="app-mobile-menu-button"
            onClick={() => setMobileMenuOpen(true)}
            aria-label="打开菜单"
          />
          <div className="app-page-heading">
            <span className="app-page-title">{currentMenu?.label || '工作台'}</span>
            <span className="app-page-path">{pageDescription}</span>
          </div>
          <div className="app-topbar-actions">
            <Button type="text" icon={<BellOutlined />} className="app-icon-button" />
            <Dropdown
              menu={{ items: languageMenuItems, onClick: handleLanguageChange }}
              placement="bottomRight"
              trigger={['click']}
              overlayClassName="app-language-dropdown"
            >
              <Button type="text" className="app-language-button">
                <span>{language === 'zh' ? 'CN' : 'US'}</span>
                <span>{language === 'zh' ? 'ZH' : 'EN'}</span>
                <DownOutlined />
              </Button>
            </Dropdown>
            <Dropdown menu={{ items: userMenuItems }} placement="bottomRight">
              <Button type="text" icon={<UserOutlined />} className="app-user-button">
                {user?.displayName || '用户'}
              </Button>
            </Dropdown>
          </div>
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
