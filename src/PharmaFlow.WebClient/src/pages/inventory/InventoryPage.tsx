import { useEffect, useState, useCallback } from 'react';
import { Table, Input, Button, Space, Tag, Tabs } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import apiClient from '../../api/client';
import type { ApiResponse, PagedResult, InventoryDto } from '../../types';

export default function InventoryPage() {
  const [data, setData] = useState<InventoryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [tab, setTab] = useState('all');
  const [searchCust, setSearchCust] = useState('');
  const [searchBatch, setSearchBatch] = useState('');
  const [sqCust, setSqCust] = useState('');
  const [sqBatch, setSqBatch] = useState('');
  const [filteredData, setFilteredData] = useState<InventoryDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [lowStockCount, setLowStockCount] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await apiClient.get<ApiResponse<PagedResult<InventoryDto>>>('/inventory', {
        params: { page: 0, size: 99999, search: sqCust || undefined, batchNo: sqBatch || undefined, lowStock: tab === 'lowStock' ? true : undefined },
      });
      setData(res.data!.items);
      if (tab === 'lowStock') setLowStockCount(res.data!.items.length);
    } finally { setLoading(false); }
  }, [sqCust, sqBatch, tab]);

  useEffect(() => { load(); }, [tab]);

  // 单独加载总数和低库存数
  useEffect(() => {
    apiClient.get<ApiResponse<PagedResult<InventoryDto>>>('/inventory', { params: { page: 0, size: 1 } })
      .then(res => setTotalCount(res.data!.totalCount));
    apiClient.get<ApiResponse<PagedResult<InventoryDto>>>('/inventory', { params: { page: 0, size: 1, lowStock: true } })
      .then(res => setLowStockCount(res.data!.totalCount));
  }, []);

  const columns: ColumnsType<InventoryDto> = [
    { title: '药品名称', dataIndex: 'drugName', width: 140,
      filters: [...new Set((filteredData.length > 0 ? filteredData : data).map(d => d.drugName).filter(Boolean))].sort().map(n => ({ text: n, value: n })),
      onFilter: (v, r) => r.drugName === (v as string),
    },
    { title: '规格', dataIndex: 'specification', width: 100,
      filters: [...new Set((filteredData.length > 0 ? filteredData : data).map(d => d.specification).filter(Boolean))].sort().map(s => ({ text: s, value: s })),
      onFilter: (v, r) => (r.specification || '') === (v as string),
    },
    { title: '批号', dataIndex: 'batchNo', width: 110 },
    { title: '库存数量', dataIndex: 'quantity', width: 80 },
    { title: '客户名称', dataIndex: 'customerName', width: 160 },
    { title: '级别', dataIndex: 'customerLevel', width: 90,
      filters: [...new Set((filteredData.length > 0 ? filteredData : data).map(d => d.customerLevel).filter(Boolean))].sort().map(l => ({ text: l, value: l })),
      onFilter: (v, r) => (r.customerLevel || '') === (v as string),
    },
    { title: '省份', dataIndex: 'province', width: 80,
      filters: [...new Set((filteredData.length > 0 ? filteredData : data).map(d => d.province).filter(Boolean))].sort().map(p => ({ text: p, value: p })),
      onFilter: (v, r) => (r.province || '') === (v as string),
    },
    { title: '状态', width: 80, render: (_, r) => (
        r.isLowStock ? <Tag color="orange">低库存</Tag> : <Tag color="green">正常</Tag>
    )},
  ];

  return (
    <div>
      <h2 style={{ marginBottom: 16 }}>📦 库存管理（经销商）</h2>
      <Tabs activeKey={tab} onChange={setTab} items={[
        { key: 'all', label: `全部 (${totalCount})` },
        { key: 'lowStock', label: `低库存预警 (${lowStockCount})` },
      ]} style={{ marginBottom: 8 }} />
      <Space wrap style={{ marginBottom: 16 }}>
        <Input placeholder="客户名称" value={searchCust} onChange={e => setSearchCust(e.target.value)}
          onPressEnter={() => { setSqCust(searchCust); setSqBatch(searchBatch); }} prefix={<SearchOutlined />} style={{ width: 180 }} allowClear />
        <Input placeholder="批号" value={searchBatch} onChange={e => setSearchBatch(e.target.value)}
          onPressEnter={() => { setSqCust(searchCust); setSqBatch(searchBatch); }} style={{ width: 140 }} allowClear />
        <Button type="primary" icon={<SearchOutlined />} onClick={() => { setSqCust(searchCust); setSqBatch(searchBatch); }}>搜索</Button>
      </Space>
      <Table rowKey={(r,i) => `${r.drugCode}-${r.batchNo}-${r.customerName}-${i}`} columns={columns}
        dataSource={data} loading={loading}
        onChange={(_p, _f, _s, extra) => setFilteredData(extra.currentDataSource as InventoryDto[])}
        pagination={{ defaultPageSize: 10, showTotal: () => `共 ${filteredData.length || data.length} 条`, showSizeChanger: true }} />
    </div>
  );
}
