import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ConfigProvider } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import AppLayout from './components/AppLayout';
import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/DashboardPage';
import DrugListPage from './pages/drugs/DrugListPage';
import CustomerListPage from './pages/customers/CustomerListPage';
import InventoryPage from './pages/inventory/InventoryPage';
import PurchaseListPage from './pages/purchases/PurchaseListPage';
import SalesListPage from './pages/sales/SalesListPage';
import DistributionPage from './pages/distribution/DistributionPage';
import ConflictPage from './pages/distribution/ConflictPage';
import ReportPage from './pages/reports/ReportPage';
import UserListPage from './pages/users/UserListPage';
import CollectionPage from './pages/datacollection/CollectionPage';

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const token = localStorage.getItem('token');
  if (!token) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

export default function App() {
  return (
    <ConfigProvider
      locale={zhCN}
      theme={{
        token: {
          colorPrimary: '#1677ff',
          colorSuccess: '#16a34a',
          colorWarning: '#d97706',
          colorError: '#dc2626',
          colorInfo: '#0f766e',
          borderRadius: 6,
          fontFamily: "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
        },
        components: {
          Card: {
            borderRadiusLG: 6,
            paddingLG: 20,
          },
          Table: {
            headerBg: '#f8fafc',
            headerColor: '#334155',
            rowHoverBg: '#f8fbff',
          },
          Menu: {
            itemBorderRadius: 6,
            itemMarginInline: 10,
          },
        },
      }}
    >
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/" element={<PrivateRoute><AppLayout /></PrivateRoute>}>
            <Route index element={<Navigate to="/dashboard" replace />} />
            <Route path="dashboard" element={<DashboardPage />} />
            <Route path="drugs" element={<DrugListPage />} />
            <Route path="customers" element={<CustomerListPage />} />
            <Route path="purchases" element={<PurchaseListPage />} />
            <Route path="sales" element={<SalesListPage />} />
            <Route path="inventory" element={<InventoryPage />} />
            <Route path="distribution" element={<DistributionPage />} />
            <Route path="distribution/conflicts" element={<ConflictPage />} />
            <Route path="reports" element={<ReportPage />} />
            <Route path="users" element={<UserListPage />} />
            <Route path="datacollection" element={<CollectionPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </ConfigProvider>
  );
}
