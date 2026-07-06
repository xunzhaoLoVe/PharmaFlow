import { useEffect, useMemo, useState } from 'react';
import { Button, DatePicker, Empty, Input, Row, Col, Select, Space, Spin, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  ComposedChart,
  Legend,
  Line,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import {
  BarChartOutlined,
  DollarOutlined,
  InboxOutlined,
  MedicineBoxOutlined,
  ReloadOutlined,
  SearchOutlined,
  TeamOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { getReportOverview } from '../../api/reports';
import type {
  EndCustomerSale,
  ReportDimensionItem,
  ReportInventoryRisk,
  ReportOverview,
  ReportRankItem,
} from '../../api/reports';

const { RangePicker } = DatePicker;

const COLORS = ['#3B82F6', '#10B981', '#F59E0B', '#8B5CF6', '#06B6D4', '#EF4444', '#64748B', '#14B8A6'];
const FILTER_KEY = 'professional_report_filters';

interface ReportFilters {
  startDate: string;
  endDate: string;
  granularity: string;
  drugName: string;
  customerName: string;
}

const defaultFilters = (): ReportFilters => ({
  startDate: dayjs().subtract(29, 'day').format('YYYY-MM-DD'),
  endDate: dayjs().format('YYYY-MM-DD'),
  granularity: 'day',
  drugName: '',
  customerName: '',
});

function loadFilters(): ReportFilters {
  try {
    const saved = sessionStorage.getItem(FILTER_KEY);
    return saved ? { ...defaultFilters(), ...JSON.parse(saved) } : defaultFilters();
  } catch {
    return defaultFilters();
  }
}

function saveFilters(filters: ReportFilters) {
  sessionStorage.setItem(FILTER_KEY, JSON.stringify(filters));
}

function n(value?: number) {
  return (value ?? 0).toLocaleString('zh-CN', { maximumFractionDigits: 2 });
}

function money(value?: number) {
  return `¥${(value ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

const panelStyle: React.CSSProperties = {
  background: '#fff',
  border: '1px solid #E2E8F0',
  borderRadius: 8,
  boxShadow: '0 2px 8px rgba(15, 23, 42, 0.05)',
};

function MetricCard({
  title,
  value,
  sub,
  icon,
  color,
  loading,
}: {
  title: string;
  value: string;
  sub?: string;
  icon: React.ReactNode;
  color: string;
  loading: boolean;
}) {
  return (
    <div style={{ ...panelStyle, padding: '16px 18px', minHeight: 92 }}>
      <Spin spinning={loading}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
          <div style={{
            width: 36,
            height: 36,
            borderRadius: 8,
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            background: `${color}18`,
            color,
            fontSize: 18,
          }}>
            {icon}
          </div>
          <div style={{ minWidth: 0 }}>
            <div style={{ color: '#64748B', fontSize: 12, lineHeight: 1.3 }}>{title}</div>
            <div style={{ color: '#0F172A', fontSize: 21, fontWeight: 800, lineHeight: 1.25 }}>{value}</div>
            {sub && <div style={{ color: '#94A3B8', fontSize: 12, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{sub}</div>}
          </div>
        </div>
      </Spin>
    </div>
  );
}

function Panel({ title, action, children }: { title: string; action?: React.ReactNode; children: React.ReactNode }) {
  return (
    <div style={{ ...panelStyle, padding: 16, height: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14, gap: 12 }}>
        <div style={{ color: '#0F172A', fontSize: 14, fontWeight: 800 }}>{title}</div>
        {action}
      </div>
      {children}
    </div>
  );
}

function DistributionPanel({ title, data }: { title: string; data: ReportDimensionItem[] }) {
  const tableColumns: ColumnsType<ReportDimensionItem> = [
    { title: '名称', dataIndex: 'name', ellipsis: true },
    { title: '数量', dataIndex: 'quantity', width: 82, align: 'right', render: v => n(v) },
    { title: '金额', dataIndex: 'amount', width: 98, align: 'right', render: v => <span style={{ color: '#059669', fontWeight: 600 }}>{money(v)}</span> },
    { title: '占比', dataIndex: 'percent', width: 68, align: 'right', render: v => `${n(v)}%` },
  ];

  return (
    <Panel title={title}>
      {data.length === 0 ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} /> : (
        <Row gutter={12} align="middle">
          <Col span={8}>
            <ResponsiveContainer width="100%" height={210}>
              <PieChart>
                <Pie data={data} dataKey="quantity" nameKey="name" innerRadius={56} outerRadius={88} paddingAngle={2}>
                  {data.map((_, index) => <Cell key={index} fill={COLORS[index % COLORS.length]} />)}
                </Pie>
                <Tooltip formatter={(value: any) => n(Number(value))} />
              </PieChart>
            </ResponsiveContainer>
          </Col>
          <Col span={16}>
            <Table
              size="small"
              rowKey="name"
              columns={tableColumns}
              dataSource={data}
              pagination={false}
            />
          </Col>
        </Row>
      )}
    </Panel>
  );
}

function RankingList({ data }: { data: ReportRankItem[] }) {
  return (
    <div style={{ display: 'grid', gap: 10 }}>
      {data.length === 0 && <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} />}
      {data.map((item, index) => (
        <div key={`${item.name}-${index}`} style={{ display: 'grid', gridTemplateColumns: '28px 1fr auto', alignItems: 'center', gap: 10 }}>
          <span style={{ color: index < 3 ? '#fff' : '#64748B', background: index < 3 ? COLORS[index] : '#F1F5F9', borderRadius: 6, height: 24, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontWeight: 700 }}>
            {index + 1}
          </span>
          <div style={{ minWidth: 0 }}>
            <div style={{ color: '#0F172A', fontWeight: 700, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{item.name}</div>
            <div style={{ color: '#94A3B8', fontSize: 12 }}>{item.subName || '未填写'} · {item.count} 笔</div>
          </div>
          <div style={{ textAlign: 'right' }}>
            <div style={{ color: '#0F172A', fontWeight: 800 }}>{n(item.quantity)}</div>
            <div style={{ color: '#059669', fontSize: 12 }}>{money(item.amount)}</div>
          </div>
        </div>
      ))}
    </div>
  );
}

export default function ReportPage() {
  const [filters, setFilters] = useState<ReportFilters>(() => loadFilters());
  const [draft, setDraft] = useState<ReportFilters>(() => loadFilters());
  const [report, setReport] = useState<ReportOverview | null>(null);
  const [loading, setLoading] = useState(false);

  const load = async (next = filters) => {
    setLoading(true);
    try {
      const data = await getReportOverview({
        startDate: next.startDate,
        endDate: next.endDate,
        granularity: next.granularity,
        drugName: next.drugName || undefined,
        customerName: next.customerName || undefined,
      });
      setReport(data);
      saveFilters(next);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(filters); }, []);

  const applyFilters = () => {
    setFilters(draft);
    load(draft);
  };

  const resetFilters = () => {
    const next = defaultFilters();
    setDraft(next);
    setFilters(next);
    load(next);
  };

  const metrics = report?.metrics;
  const riskColumns: ColumnsType<ReportInventoryRisk> = [
    { title: '风险', dataIndex: 'riskType', width: 105, render: v => <Tag color={String(v).includes('低库存') ? 'orange' : 'purple'}>{v}</Tag> },
    { title: '药品', dataIndex: 'drugName', ellipsis: true },
    { title: '规格', dataIndex: 'specification', width: 110, ellipsis: true },
    { title: '批号', dataIndex: 'batchNo', width: 110 },
    { title: '客户/库存点', dataIndex: 'customerName', width: 150, ellipsis: true, render: v => v || '本公司' },
    { title: '库存', dataIndex: 'quantity', width: 90, align: 'right', render: v => n(v) },
    { title: '预警线', dataIndex: 'warningLowQuantity', width: 80, align: 'right' },
    { title: '有效期', dataIndex: 'expiryDate', width: 110, render: v => v ? dayjs(v).format('YYYY-MM-DD') : '-' },
  ];

  const terminalColumns: ColumnsType<EndCustomerSale> = [
    { title: '终端客户', dataIndex: 'customerName', ellipsis: true },
    { title: '省份', dataIndex: 'province', width: 90, render: v => v || '-' },
    { title: '药品名称', dataIndex: 'drugName', ellipsis: true },
    { title: '规格', dataIndex: 'specification', width: 130, ellipsis: true },
    { title: '终端销售数量', dataIndex: 'totalQuantity', width: 130, align: 'right', sorter: (a, b) => a.totalQuantity - b.totalQuantity, render: v => <b style={{ color: '#2563EB' }}>{n(v)}</b> },
  ];

  const provinceChartData = useMemo(() => report?.provinceDistribution.slice(0, 10) ?? [], [report]);
  const trendData = report?.trend ?? [];

  return (
    <div style={{ display: 'grid', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <h2 style={{ margin: 0, color: '#0F172A', fontSize: 22 }}>报表统计</h2>
          <div style={{ color: '#64748B', marginTop: 4, fontSize: 13 }}>
            {report ? `${dayjs(report.startDate).format('YYYY-MM-DD')} 至 ${dayjs(report.endDate).format('YYYY-MM-DD')}` : '加载中'}
          </div>
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => load(filters)} loading={loading}>刷新</Button>
        </Space>
      </div>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} xl={6}>
          <MetricCard loading={loading} title="一级经销进货" value={n(metrics?.dealerPurchaseQuantity ?? metrics?.purchaseQuantity)} sub={`进货金额 ${money(metrics?.purchaseAmount)}`} color="#F59E0B" icon={<MedicineBoxOutlined />} />
        </Col>
        <Col xs={24} sm={12} xl={6}>
          <MetricCard loading={loading} title="卖到终端" value={n(metrics?.dealerTerminalSalesQuantity ?? metrics?.salesQuantity)} sub={`终端销售金额 ${money(metrics?.salesAmount)}`} color="#3B82F6" icon={<BarChartOutlined />} />
        </Col>
        <Col xs={24} sm={12} xl={6}>
          <MetricCard loading={loading} title="经销商库存" value={n(metrics?.dealerInventoryQuantity ?? metrics?.inventoryQuantity)} sub={`调拨数量 ${n(metrics?.dealerTransferQuantity)} · 药品 ${metrics?.drugCount ?? 0}`} color="#10B981" icon={<InboxOutlined />} />
        </Col>
        <Col xs={24} sm={12} xl={6}>
          <MetricCard loading={loading} title="渠道风险预警" value={`${(metrics?.lowStockCount ?? 0) + (metrics?.expiringCount ?? 0)}`} sub={`经销商低库存 ${metrics?.lowStockCount ?? 0} · 近效期 ${metrics?.expiringCount ?? 0}`} color="#8B5CF6" icon={<WarningOutlined />} />
        </Col>
      </Row>

      <div style={{ ...panelStyle, padding: 14 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
          <Space wrap>
            <span style={{ color: '#334155', fontWeight: 700 }}>时间范围:</span>
            <RangePicker
              value={[dayjs(draft.startDate), dayjs(draft.endDate)]}
              onChange={(dates) => {
                if (!dates?.[0] || !dates?.[1]) return;
                setDraft({ ...draft, startDate: dates[0].format('YYYY-MM-DD'), endDate: dates[1].format('YYYY-MM-DD') });
              }}
              allowClear={false}
            />
            <span style={{ color: '#334155', fontWeight: 700 }}>粒度:</span>
            <Select
              value={draft.granularity}
              style={{ width: 110 }}
              onChange={granularity => setDraft({ ...draft, granularity })}
              options={[
                { label: '按日', value: 'day' },
                { label: '按周', value: 'week' },
                { label: '按月', value: 'month' },
              ]}
            />
            <Input placeholder="药品名称" allowClear value={draft.drugName} onChange={e => setDraft({ ...draft, drugName: e.target.value })} style={{ width: 180 }} />
            <Input placeholder="客户/供应商" allowClear value={draft.customerName} onChange={e => setDraft({ ...draft, customerName: e.target.value })} style={{ width: 180 }} />
          </Space>
          <Space>
            <Button icon={<ReloadOutlined />} onClick={resetFilters}>重置</Button>
            <Button type="primary" icon={<SearchOutlined />} onClick={applyFilters} loading={loading}>查询</Button>
          </Space>
        </div>
      </div>

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={12}>
          <DistributionPanel title="终端药品销售分布" data={report?.drugDistribution ?? []} />
        </Col>
        <Col xs={24} xl={12}>
          <DistributionPanel title="终端客户销售分布" data={report?.customerDistribution ?? []} />
        </Col>
        <Col xs={24} xl={12}>
          <Panel title="区域流向分布">
            {provinceChartData.length === 0 ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} /> : (
              <ResponsiveContainer width="100%" height={290}>
                <BarChart data={provinceChartData} layout="vertical" margin={{ left: 12, right: 28 }}>
                  <CartesianGrid stroke="#E2E8F0" horizontal={false} />
                  <XAxis type="number" tick={{ fill: '#64748B', fontSize: 11 }} />
                  <YAxis dataKey="name" type="category" width={82} tick={{ fill: '#334155', fontSize: 12 }} />
                  <Tooltip formatter={(value: any) => n(Number(value))} />
                  <Bar dataKey="quantity" radius={[0, 8, 8, 0]} fill="#10B981" />
                </BarChart>
              </ResponsiveContainer>
            )}
          </Panel>
        </Col>
        <Col xs={24} xl={12}>
          <Panel title="客户结构">
            <ResponsiveContainer width="100%" height={290}>
              <PieChart>
                <Pie data={report?.dealerLevelDistribution ?? []} dataKey="count" nameKey="name" innerRadius={62} outerRadius={96}>
                  {(report?.dealerLevelDistribution ?? []).map((_, index) => <Cell key={index} fill={COLORS[index % COLORS.length]} />)}
                </Pie>
                <Tooltip formatter={(value: any) => `${value} 家`} />
                <Legend />
              </PieChart>
            </ResponsiveContainer>
          </Panel>
        </Col>
      </Row>

      <Panel title="一级进货 / 终端销售 / 渠道流向趋势">
        <ResponsiveContainer width="100%" height={330}>
          <ComposedChart data={trendData} margin={{ left: 8, right: 20, top: 10, bottom: 4 }}>
            <CartesianGrid stroke="#E2E8F0" />
            <XAxis dataKey="period" tick={{ fill: '#64748B', fontSize: 11 }} />
            <YAxis tick={{ fill: '#64748B', fontSize: 11 }} />
            <Tooltip formatter={(value: any) => n(Number(value))} />
            <Legend />
            <Area type="monotone" dataKey="flowQuantity" name="流向数量" stroke="#06B6D4" fill="#CFFAFE" strokeWidth={2} />
            <Bar dataKey="purchaseQuantity" name="一级进货" fill="#F59E0B" radius={[4, 4, 0, 0]} />
            <Line type="monotone" dataKey="salesQuantity" name="卖到终端" stroke="#3B82F6" strokeWidth={3} dot={{ r: 3 }} />
          </ComposedChart>
        </ResponsiveContainer>
      </Panel>

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={12}>
          <Panel title="畅销药品排行">
            <RankingList data={report?.topDrugs ?? []} />
          </Panel>
        </Col>
        <Col xs={24} xl={12}>
          <Panel title="重点客户排行">
            <RankingList data={report?.topCustomers ?? []} />
          </Panel>
        </Col>
      </Row>

      <Panel title="库存风险明细">
        <Table
          rowKey={(r, i) => `${r.drugName}-${r.batchNo}-${i}`}
          columns={riskColumns}
          dataSource={report?.inventoryRisks ?? []}
          loading={loading}
          size="small"
          pagination={{ pageSize: 8, showSizeChanger: false }}
        />
      </Panel>

      <Panel title="终端客户销售明细">
        <Table
          rowKey={(r, i) => `${r.customerName}-${r.drugName}-${r.specification}-${i}`}
          columns={terminalColumns}
          dataSource={report?.endCustomerSales ?? []}
          loading={loading}
          size="small"
          pagination={{ pageSize: 12, showSizeChanger: true }}
        />
      </Panel>
    </div>
  );
}
