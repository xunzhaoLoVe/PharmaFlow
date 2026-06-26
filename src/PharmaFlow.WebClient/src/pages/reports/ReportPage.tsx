import { useEffect, useState, useMemo } from 'react';
import { Table, Card, Input, Button, Select, Row, Col, Statistic, DatePicker } from 'antd';
import { SearchOutlined, MedicineBoxOutlined, TeamOutlined, AppstoreOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import apiClient from '../../api/client';
import type { ApiResponse } from '../../types';

const { RangePicker } = DatePicker;
const STORAGE_KEY = 'report_filters';

// 从 sessionStorage 恢复筛选状态
function loadFilters(): { drug?: string; customer: string; start?: string; end?: string } {
  try {
    const saved = sessionStorage.getItem(STORAGE_KEY);
    if (saved) return JSON.parse(saved);
  } catch {}
  return { customer: '' };
}

function saveFilters(f: { drug?: string; customer: string; start?: string; end?: string }) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(f));
}

interface EndCustomerSaleDto {
  customerName: string;
  province?: string;
  drugName: string;
  specification?: string;
  totalQuantity: number;
}

export default function ReportPage() {
  // 从 sessionStorage 恢复筛选状态
  const saved = loadFilters();
  const [filterDrug, setFilterDrug] = useState<string | undefined>(saved.drug);
  const [filterCustomer, setFilterCustomer] = useState(saved.customer || '');
  const [sqCustomer, setSqCustomer] = useState(saved.customer || '');
  const [dateRange, setDateRange] = useState<[string, string] | null>(
    saved.start && saved.end ? [saved.start, saved.end] : null
  );

  const [data, setData] = useState<EndCustomerSaleDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [drugs, setDrugs] = useState<string[]>([]);
  const [filteredCount, setFilteredCount] = useState(0);

  // 持久化筛选
  const updateFilter = (patch: Partial<typeof saved>) => {
    const current = { drug: filterDrug, customer: filterCustomer, start: dateRange?.[0], end: dateRange?.[1] };
    const next = { ...current, ...patch };
    Object.keys(next).forEach(k => { if (!next[k as keyof typeof next]) delete next[k as keyof typeof next]; });
    saveFilters(next);
  };

  // 加载数据
  const load = async () => {
    setLoading(true);
    try {
      const params: any = {};
      if (filterDrug) params.drugName = filterDrug;
      if (dateRange) { params.startDate = dateRange[0]; params.endDate = dateRange[1]; }
      const res = await apiClient.get<ApiResponse<EndCustomerSaleDto[]>>('/reports/end-customer-sales', { params });
      setData((res as any).data || []);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [filterDrug, dateRange]);

  // 提取所有药品名称
  useEffect(() => {
    if (data.length > 0) {
      setDrugs([...new Set(data.map(d => d.drugName))].sort());
    }
  }, [data]);

  // 客户端搜索过滤
  const filtered = useMemo(() => {
    return data.filter(d => {
      if (sqCustomer && !d.customerName.includes(sqCustomer)) return false;
      return true;
    });
  }, [data, sqCustomer]);

  // 汇总统计
  const totalQuantity = useMemo(() => filtered.reduce((s, d) => s + d.totalQuantity, 0), [filtered]);
  const totalCustomers = useMemo(() => new Set(filtered.map(d => d.customerName)).size, [filtered]);
  const totalDrugs = useMemo(() => new Set(filtered.map(d => d.drugName)).size, [filtered]);

  const columns: ColumnsType<EndCustomerSaleDto> = [
    { title: '终端客户', dataIndex: 'customerName', width: 180, sorter: (a, b) => a.customerName.localeCompare(b.customerName) },
    { title: '省份', dataIndex: 'province', width: 90,
      filters: [...new Set(data.map(d => d.province).filter(Boolean))].sort().map(p => ({ text: p, value: p })),
      onFilter: (v, r) => r.province === (v as string),
    },
    { title: '药品名称', dataIndex: 'drugName', width: 160,
      filters: [...new Set(data.map(d => d.drugName))].sort().map(n => ({ text: n, value: n })),
      onFilter: (v, r) => r.drugName === (v as string),
    },
    { title: '规格', dataIndex: 'specification', width: 100,
      filters: [...new Set(data.map(d => d.specification).filter(Boolean))].sort().map(s => ({ text: s, value: s })),
      onFilter: (v, r) => (r.specification || '') === (v as string),
    },
    { title: '销售数量', dataIndex: 'totalQuantity', width: 110, sorter: (a, b) => a.totalQuantity - b.totalQuantity,
      render: v => <span style={{ fontWeight: 600, color: '#1976D2' }}>{v.toLocaleString()}</span>,
    },
  ];

  return (
    <div>
      <h2 style={{ marginBottom: 24 }}>📈 报表统计</h2>

      {/* 明细表格 */}
      <Card title="终端客户销售明细">
        {/* 汇总卡片 — 标题下方 */}
        <Row gutter={16} style={{ marginBottom: 16 }}>
          <Col span={8}>
            <Card loading={loading} size="small">
              <Statistic title="终端客户数" value={totalCustomers} prefix={<TeamOutlined />} suffix="家" valueStyle={{ color: '#1976D2', fontSize: 20 }} />
            </Card>
          </Col>
          <Col span={8}>
            <Card loading={loading} size="small">
              <Statistic title="药品品种数" value={totalDrugs} prefix={<AppstoreOutlined />} suffix="种" valueStyle={{ color: '#4CAF50', fontSize: 20 }} />
            </Card>
          </Col>
          <Col span={8}>
            <Card loading={loading} size="small">
              <Statistic title="销售总数量" value={totalQuantity} prefix={<MedicineBoxOutlined />} valueStyle={{ color: '#FF9800', fontSize: 20 }} />
            </Card>
          </Col>
        </Row>

        {/* 筛选栏 */}
        <div style={{ marginBottom: 16, display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <RangePicker
            placeholder={['开始日期', '结束日期']}
            value={dateRange ? [dayjs(dateRange[0]), dayjs(dateRange[1])] : null}
            onChange={(dates) => {
              if (dates && dates[0] && dates[1]) {
                setDateRange([dates[0].format('YYYY-MM-DD'), dates[1].format('YYYY-MM-DD')]);
                updateFilter({ start: dates[0].format('YYYY-MM-DD'), end: dates[1].format('YYYY-MM-DD') });
              } else {
                setDateRange(null);
                updateFilter({ start: undefined, end: undefined });
              }
            }}
            style={{ width: 260 }}
            allowClear
          />
          <Select
            placeholder="按药品筛选"
            value={filterDrug}
            onChange={(v) => { setFilterDrug(v); updateFilter({ drug: v }); }}
            allowClear
            showSearch
            style={{ width: 200 }}
            filterOption={(v, o) => (o?.label as string || '').includes(v)}
            options={drugs.map(d => ({ label: d, value: d }))}
          />
          <Input
            placeholder="搜索终端客户名称"
            value={filterCustomer}
            onChange={e => { setFilterCustomer(e.target.value); updateFilter({ customer: e.target.value }); }}
            onPressEnter={() => setSqCustomer(filterCustomer)}
            prefix={<SearchOutlined />}
            style={{ width: 220 }}
            allowClear
          />
          <Button type="primary" icon={<SearchOutlined />} onClick={() => setSqCustomer(filterCustomer)}>搜索</Button>
        </div>
        <Table
          rowKey={(r, i) => `${r.customerName}-${r.drugName}-${i}`}
          columns={columns}
          dataSource={filtered}
          loading={loading}
          onChange={(_p, _f, _s, extra) => setFilteredCount(extra.currentDataSource.length)}
          pagination={{
            defaultPageSize: 20,
            showTotal: () => `共 ${filteredCount || filtered.length} 条`,
            showSizeChanger: true,
            pageSizeOptions: ['10', '20', '50', '100'],
          }}
          scroll={{ y: 500 }}
        />
      </Card>
    </div>
  );
}
