import { Card, Row, Col, Statistic } from 'antd';
import {
  MedicineBoxOutlined, AppstoreOutlined,
  ShopOutlined, WarningOutlined,
} from '@ant-design/icons';

export default function ReportPage() {
  return (
    <div>
      <h2>📈 报表统计</h2>
      <Row gutter={16}>
        <Col span={6}>
          <Card><Statistic title="药品品种" value={0} prefix={<MedicineBoxOutlined />} valueStyle={{ color: '#1976D2' }} /></Card>
        </Col>
        <Col span={6}>
          <Card><Statistic title="药品分类" value={0} prefix={<AppstoreOutlined />} valueStyle={{ color: '#4CAF50' }} /></Card>
        </Col>
        <Col span={6}>
          <Card><Statistic title="生产厂家" value={0} prefix={<ShopOutlined />} valueStyle={{ color: '#FF9800' }} /></Card>
        </Col>
        <Col span={6}>
          <Card><Statistic title="库存预警" value={0} prefix={<WarningOutlined />} valueStyle={{ color: '#F44336' }} /></Card>
        </Col>
      </Row>
      <Card style={{ marginTop: 24 }}>
        <div style={{ textAlign: 'center', padding: 40, color: '#999' }}>
          📊 报表统计模块 — 数据就绪，等待报表需求
        </div>
      </Card>
    </div>
  );
}
