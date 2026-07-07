import { useEffect, useState, useCallback } from 'react';
import { Table, Input, Button, Space, Tag, Tabs } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import apiClient from '../../api/client';
import TableFilters from '../../components/TableFilters';
import { applyTableFilters, type TableFilterField, type TableFilterValues } from '../../utils/tableFilters';
import { numberSorter, textSorter } from '../../utils/tableSorters';
import type { ApiResponse, PagedResult, InventoryDto } from '../../types';

export default function InventoryPage() {
  const [data, setData] = useState<InventoryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [tab, setTab] = useState('all');
  const [searchCust, setSearchCust] = useState('');
  const [searchBatch, setSearchBatch] = useState('');
  const [sqCust, setSqCust] = useState('');
  const [sqBatch, setSqBatch] = useState('');
  const [tableFilters, setTableFilters] = useState<TableFilterValues>({});
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
  useEffect(() => { setTableFilters({}); }, [tab]);

  // 单独加载总数和低库存数
  useEffect(() => {
    apiClient.get<ApiResponse<PagedResult<InventoryDto>>>('/inventory', { params: { page: 0, size: 1 } })
      .then(res => setTotalCount(res.data!.totalCount));
    apiClient.get<ApiResponse<PagedResult<InventoryDto>>>('/inventory', { params: { page: 0, size: 1, lowStock: true } })
      .then(res => setLowStockCount(res.data!.totalCount));
  }, []);

  const columns: ColumnsType<InventoryDto> = [
    { title: '药品名称', dataIndex: 'drugName', width: 140,
      sorter: textSorter<InventoryDto>(r => r.drugName),
    },
    { title: '规格', dataIndex: 'specification', width: 100,
      sorter: textSorter<InventoryDto>(r => r.specification),
    },
    { title: '批号', dataIndex: 'batchNo', width: 110, sorter: textSorter<InventoryDto>(r => r.batchNo) },
    { title: '库存数量', dataIndex: 'quantity', width: 80, sorter: numberSorter<InventoryDto>(r => r.quantity) },
    { title: '客户名称', dataIndex: 'customerName', width: 160, sorter: textSorter<InventoryDto>(r => r.customerName) },
    { title: '级别', dataIndex: 'customerLevel', width: 90,
      sorter: textSorter<InventoryDto>(r => r.customerLevel),
    },
    { title: '省份', dataIndex: 'province', width: 80,
      sorter: textSorter<InventoryDto>(r => r.province),
    },
    { title: '状态', width: 80, sorter: numberSorter<InventoryDto>(r => r.isLowStock ? 1 : 0), render: (_, r) => (
        r.isLowStock ? <Tag color="orange">低库存</Tag> : <Tag color="green">正常</Tag>
    )},
  ];
  const filterFields: TableFilterField<InventoryDto>[] = [
    { key: 'drugName', label: '药品名称', getValue: r => r.drugName },
    { key: 'specification', label: '规格', getValue: r => r.specification },
    { key: 'batchNo', label: '批号', getValue: r => r.batchNo },
    { key: 'customerName', label: '客户名称', getValue: r => r.customerName },
    { key: 'customerLevel', label: '级别', getValue: r => r.customerLevel },
    { key: 'province', label: '省份', getValue: r => r.province },
    {
      key: 'status',
      label: '状态',
      getValue: r => r.isLowStock ? 'lowStock' : 'normal',
      options: [
        { label: '低库存', value: 'lowStock' },
        { label: '正常', value: 'normal' },
      ],
    },
  ];
  const tableData = applyTableFilters(data, filterFields, tableFilters);

  return (
    <div>
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
        <TableFilters rows={data} fields={filterFields} value={tableFilters} onChange={setTableFilters} />
      </Space>
      <Table rowKey={(r,i) => `${r.drugCode}-${r.batchNo}-${r.customerName}-${i}`} columns={columns}
        dataSource={tableData} loading={loading}
        pagination={{ defaultPageSize: 10, showTotal: () => `共 ${tableData.length} 条`, showSizeChanger: true }} />
    </div>
  );
}
