import { useEffect, useMemo, useState } from 'react';
import { Card, Col, Input, Row, Space, Table, Tag, Typography } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import apiClient from '../../api/client';
import TableFilters from '../../components/TableFilters';
import { applyTableFilters, type TableFilterField, type TableFilterValues } from '../../utils/tableFilters';
import { numberSorter, textSorter } from '../../utils/tableSorters';
import type { ApiResponse, ChannelConflictDto } from '../../types';

const severityColor: Record<string, string> = {
  高: 'red',
  中: 'orange',
  低: 'blue',
};

const warningTypeColor: Record<string, string> = {
  跨区域流向: 'volcano',
  终端突增: 'red',
  短期囤货: 'gold',
  层级倒挂: 'magenta',
  同级调拨: 'purple',
  批号多省扩散: 'cyan',
  一级跨省扩散: 'cyan',
};

function formatDate(value?: string) {
  return value ? value.substring(0, 10) : '-';
}

function formatNumber(value?: number | null) {
  return value == null ? '-' : value.toLocaleString('zh-CN', { maximumFractionDigits: 2 });
}

export default function ConflictPage() {
  const [data, setData] = useState<ChannelConflictDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [tableFilters, setTableFilters] = useState<TableFilterValues>({});

  const load = async (batchNo?: string) => {
    setLoading(true);
    try {
      const res = await apiClient.get<ApiResponse<ChannelConflictDto[]>>('/distribution/conflicts', { params: { batchNo: batchNo || undefined } });
      const payload = res as unknown as ApiResponse<ChannelConflictDto[]> | ChannelConflictDto[];
      setData(Array.isArray(payload) ? payload : payload.data || []);
    } finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []);

  const stats = useMemo(() => {
    const high = data.filter(item => item.severity === '高').length;
    const medium = data.filter(item => item.severity === '中').length;
    const types = new Set(data.map(item => item.warningType).filter(Boolean)).size;
    return { high, medium, types };
  }, [data]);

  const columns: ColumnsType<ChannelConflictDto> = [
    {
      title: '风险',
      dataIndex: 'severity',
      width: 72,
      fixed: 'left',
      sorter: textSorter<ChannelConflictDto>(r => r.severity),
      render: (value: string) => <Tag color={severityColor[value] || 'default'}>{value || '中'}</Tag>,
    },
    {
      title: '预警类型',
      dataIndex: 'warningType',
      width: 120,
      fixed: 'left',
      sorter: textSorter<ChannelConflictDto>(r => r.warningType),
      render: (value: string) => <Tag color={warningTypeColor[value] || 'default'}>{value || '串货预警'}</Tag>,
    },
    { title: '药品名称', dataIndex: 'drugName', width: 150, sorter: textSorter<ChannelConflictDto>(r => r.drugName) },
    { title: '批号', dataIndex: 'batchNo', width: 130, sorter: textSorter<ChannelConflictDto>(r => r.batchNo) },
    { title: '来源/经销商', dataIndex: 'distributor', width: 180, ellipsis: true, sorter: textSorter<ChannelConflictDto>(r => r.distributor) },
    { title: '异常客户', dataIndex: 'actualCustomer', width: 190, ellipsis: true, sorter: textSorter<ChannelConflictDto>(r => r.actualCustomer) },
    { title: '区域/基准', dataIndex: 'expectedRegion', width: 150, ellipsis: true, sorter: textSorter<ChannelConflictDto>(r => r.expectedRegion) },
    { title: '实际区域', dataIndex: 'actualRegion', width: 110, sorter: textSorter<ChannelConflictDto>(r => r.actualRegion) },
    { title: '数量', dataIndex: 'quantity', width: 90, align: 'right', sorter: numberSorter<ChannelConflictDto>(r => r.quantity), render: formatNumber },
    { title: '基准量', dataIndex: 'baselineQuantity', width: 90, align: 'right', sorter: numberSorter<ChannelConflictDto>(r => r.baselineQuantity), render: formatNumber },
    { title: '倍数', dataIndex: 'ratio', width: 76, align: 'right', sorter: numberSorter<ChannelConflictDto>(r => r.ratio), render: (v?: number) => v ? `${formatNumber(v)}x` : '-' },
    { title: '日期', dataIndex: 'date', width: 110, sorter: textSorter<ChannelConflictDto>(r => r.date), render: formatDate },
  ];

  const filterFields: TableFilterField<ChannelConflictDto>[] = [
    { key: 'severity', label: '风险等级', getValue: r => r.severity },
    { key: 'warningType', label: '预警类型', getValue: r => r.warningType },
    { key: 'drugName', label: '药品名称', getValue: r => r.drugName },
    { key: 'batchNo', label: '批号', getValue: r => r.batchNo },
    { key: 'distributor', label: '来源/经销商', getValue: r => r.distributor },
    { key: 'actualCustomer', label: '异常客户', getValue: r => r.actualCustomer },
    { key: 'actualRegion', label: '实际区域', getValue: r => r.actualRegion },
  ];
  const tableData = applyTableFilters(data, filterFields, tableFilters);

  return (
    <div>
      <Row gutter={12} style={{ marginBottom: 12 }}>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Typography.Text type="secondary">高风险预警</Typography.Text>
            <Typography.Title level={3} style={{ margin: 0, color: '#dc2626' }}>{stats.high}</Typography.Title>
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Typography.Text type="secondary">中风险预警</Typography.Text>
            <Typography.Title level={3} style={{ margin: 0, color: '#f97316' }}>{stats.medium}</Typography.Title>
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Typography.Text type="secondary">触发规则</Typography.Text>
            <Typography.Title level={3} style={{ margin: 0, color: '#0f766e' }}>{stats.types}</Typography.Title>
          </Card>
        </Col>
      </Row>

      <Space style={{ marginBottom: 16 }} wrap>
        <Input.Search
          placeholder="按批号筛选"
          enterButton
          onSearch={(value) => load(value)}
          prefix={<SearchOutlined />}
          style={{ width: 260 }}
          allowClear
        />
        <TableFilters rows={data} fields={filterFields} value={tableFilters} onChange={setTableFilters} />
      </Space>

      <Table
        rowKey={(r, i) => `${r.warningType}-${r.drugName}-${r.batchNo}-${r.actualCustomer}-${r.date}-${i}`}
        columns={columns}
        dataSource={tableData}
        loading={loading}
        pagination={{ pageSize: 20, showSizeChanger: true }}
        scroll={{ x: 1420 }}
        expandable={{
          expandedRowRender: record => (
            <Space direction="vertical" size={4}>
              <Typography.Text><strong>触发原因：</strong>{record.reason || '-'}</Typography.Text>
              <Typography.Text><strong>建议处理：</strong>{record.suggestion || '-'}</Typography.Text>
            </Space>
          ),
        }}
      />
    </div>
  );
}
