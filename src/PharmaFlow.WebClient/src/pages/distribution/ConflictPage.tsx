import { useEffect, useState } from 'react';
import { Table, Input, Space } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import apiClient from '../../api/client';
import TableFilters from '../../components/TableFilters';
import { applyTableFilters, type TableFilterField, type TableFilterValues } from '../../utils/tableFilters';
import { numberSorter, textSorter } from '../../utils/tableSorters';
import type { ApiResponse } from '../../types';

export default function ConflictPage() {
  const [data, setData] = useState<Record<string, unknown>[]>([]);
  const [loading, setLoading] = useState(false);
  const [tableFilters, setTableFilters] = useState<TableFilterValues>({});

  const load = async (batchNo?: string) => {
    setLoading(true);
    try {
      const res = await apiClient.get<ApiResponse<Record<string, unknown>[]>>('/distribution/conflicts', { params: { batchNo: batchNo || undefined } });
      setData(res.data.data || []);
    } finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []);

  const columns = [
    { title: '药品名称', dataIndex: 'drugName', width: 130, sorter: textSorter<Record<string, unknown>>(r => r.drugName) },
    { title: '批号', dataIndex: 'batchNo', width: 110, sorter: textSorter<Record<string, unknown>>(r => r.batchNo) },
    { title: '经销商', dataIndex: 'distributor', width: 140, sorter: textSorter<Record<string, unknown>>(r => r.distributor) },
    { title: '预期区域', dataIndex: 'expectedRegion', width: 100, sorter: textSorter<Record<string, unknown>>(r => r.expectedRegion) },
    { title: '实际客户', dataIndex: 'actualCustomer', width: 140, sorter: textSorter<Record<string, unknown>>(r => r.actualCustomer) },
    { title: '实际区域', dataIndex: 'actualRegion', width: 100, sorter: textSorter<Record<string, unknown>>(r => r.actualRegion) },
    { title: '数量', dataIndex: 'quantity', width: 70, sorter: numberSorter<Record<string, unknown>>(r => r.quantity) },
    { title: '日期', dataIndex: 'date', width: 100, sorter: textSorter<Record<string, unknown>>(r => r.date) },
  ];
  const filterFields: TableFilterField<Record<string, unknown>>[] = [
    { key: 'drugName', label: '药品名称', getValue: r => r.drugName },
    { key: 'batchNo', label: '批号', getValue: r => r.batchNo },
    { key: 'distributor', label: '经销商', getValue: r => r.distributor },
    { key: 'expectedRegion', label: '预期区域', getValue: r => r.expectedRegion },
    { key: 'actualCustomer', label: '实际客户', getValue: r => r.actualCustomer },
    { key: 'actualRegion', label: '实际区域', getValue: r => r.actualRegion },
  ];
  const tableData = applyTableFilters(data, filterFields, tableFilters);

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Input placeholder="按批号筛选" onPressEnter={(e) => load((e.target as HTMLInputElement).value)}
          prefix={<SearchOutlined />} style={{ width: 200 }} allowClear />
        <TableFilters rows={data} fields={filterFields} value={tableFilters} onChange={setTableFilters} />
      </Space>
      <Table rowKey={(r,i) => String(i)} columns={columns} dataSource={tableData} loading={loading} pagination={false} />
    </div>
  );
}
