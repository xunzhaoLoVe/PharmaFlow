import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ConfigProvider } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import AppLayout from './components/AppLayout';
import LandingPage from './pages/LandingPage';
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
          colorPrimary: '#3b82f6',
          colorSuccess: '#16a34a',
          colorWarning: '#f97316',
          colorError: '#dc2626',
          colorInfo: '#06b6d4',
          colorBgLayout: '#eef9f7',
          colorBgContainer: '#ffffff',
          colorBorder: '#e5edf4',
          colorText: '#111827',
          colorTextSecondary: '#64748b',
          borderRadius: 10,
          fontFamily: "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
        },
        components: {
          Card: {
            borderRadiusLG: 12,
            paddingLG: 20,
          },
          Table: {
            headerBg: '#fbfdff',
            headerColor: '#334155',
            rowHoverBg: '#f3f8ff',
            borderColor: '#edf2f7',
          },
          Menu: {
            itemBorderRadius: 8,
            itemMarginInline: 10,
            itemSelectedBg: '#e8f1ff',
            itemSelectedColor: '#2563eb',
          },
          Button: {
            borderRadius: 8,
          },
          Select: {
            borderRadius: 8,
          },
          Input: {
            borderRadius: 8,
          },
        },
      }}
    >
      <div translate="no" className="notranslate">
        <BrowserRouter>
          <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route element={<PrivateRoute><AppLayout /></PrivateRoute>}>
            <Route path="dashboard" element={<DashboardPage />} />
            <Route path="drugs" element={<DrugListPage />} />
            <Route path="customers" element={<Navigate to="/customers/distributors" replace />} />
            <Route path="customers/distributors" element={<CustomerListPage />} />
            <Route path="customers/end" element={<CustomerListPage />} />
            <Route path="purchases" element={<PurchaseListPage />} />
            <Route path="sales" element={<SalesListPage />} />
            <Route path="inventory" element={<InventoryPage />} />
            <Route path="distribution" element={<DistributionPage />} />
            <Route path="distribution/conflicts" element={<ConflictPage />} />
            <Route path="reports" element={<ReportPage />} />
            <Route path="users" element={<UserListPage />} />
            <Route path="datacollection" element={<Navigate to="/datacollection/sources" replace />} />
            <Route path="datacollection/sources" element={<CollectionPage />} />
            <Route path="datacollection/data" element={<CollectionPage />} />
            <Route path="datacollection/summary" element={<CollectionPage />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </BrowserRouter>
      </div>
    </ConfigProvider>
  );
}
