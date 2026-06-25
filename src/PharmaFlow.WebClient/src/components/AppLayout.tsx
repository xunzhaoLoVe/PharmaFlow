import { useState } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { Menu, Button } from 'antd';
import {
  DashboardOutlined, MedicineBoxOutlined, TeamOutlined,
  ShoppingCartOutlined, DollarOutlined,
  InboxOutlined, ApartmentOutlined, AlertOutlined,
  BarChartOutlined, LogoutOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
} from '@ant-design/icons';
import { useAuthStore } from '../store/authStore';

const menuItems = [
  { key: '/dashboard', icon: <DashboardOutlined />, label: '首页仪表盘' },
  { key: '/drugs', icon: <MedicineBoxOutlined />, label: '药品管理' },
  { key: '/customers', icon: <TeamOutlined />, label: '客户管理' },
  { key: '/purchases', icon: <ShoppingCartOutlined />, label: '采购管理' },
  { key: '/sales', icon: <DollarOutlined />, label: '销售管理' },
  { key: '/inventory', icon: <InboxOutlined />, label: '库存管理' },
  { key: '/distribution', icon: <ApartmentOutlined />, label: '流向查询' },
  { key: '/distribution/conflicts', icon: <AlertOutlined />, label: '串货预警' },
  { key: '/reports', icon: <BarChartOutlined />, label: '报表统计' },
];

export default function AppLayout() {
  const [collapsed, setCollapsed] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuthStore();
  const siderW = collapsed ? 80 : 230;

  const handleLogout = () => { logout(); navigate('/login'); };

  return (
    <div style={{ display: 'flex' }}>
      {/* 侧边栏 - sticky 固定 */ }
      <div style={{ width: siderW, minWidth: siderW, background: '#001529', transition: 'width 0.2s', display: 'flex', flexDirection: 'column', position: 'sticky', top: 0, height: '100vh', overflow: 'auto' }}>
        <div style={{ height: 64, display: 'flex', alignItems: 'center', justifyContent: 'center', borderBottom: '1px solid rgba(255,255,255,0.1)', flexShrink: 0 }}>
          <span style={{ color: '#fff', fontSize: collapsed ? 16 : 18, fontWeight: 'bold', whiteSpace: 'nowrap', overflow: 'hidden' }}>
            {collapsed ? '💊' : '💊 药品流向管理'}
          </span>
        </div>
        <Menu theme="dark" mode="inline" selectedKeys={[location.pathname]} items={menuItems}
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
          <span style={{ marginRight: 16, color: '#666' }}>欢迎，{user?.displayName || '用户'}</span>
          <Button type="text" icon={<LogoutOutlined />} onClick={handleLogout}>退出</Button>
        </div>
        <div style={{ flex: 1, padding: 16 }}>
          <div style={{ background: '#fff', borderRadius: 8, padding: 24, minHeight: 280 }}>
            <Outlet />
          </div>
        </div>
      </div>
    </div>
  );
}
