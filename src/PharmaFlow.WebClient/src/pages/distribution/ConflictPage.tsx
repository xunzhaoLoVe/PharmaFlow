import { useEffect, useState } from 'react';
import { Table, Input, Space } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import apiClient from '../../api/client';
import type { ApiResponse } from '../../types';

export default function ConflictPage() {
  const [data, setData] = useState<Record<string, unknown>[]>([]);
  const [loading, setLoading] = useState(false);

  const load = async (batchNo?: string) => {
    setLoading(true);
    try {
      const res = await apiClient.get<ApiResponse<Record<string, unknown>[]>>('/distribution/conflicts', { params: { batchNo: batchNo || undefined } });
      setData(res.data.data || []);
    } finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []);

  const columns = [
    { title: '药品名称', dataIndex: 'drugName', width: 130 },
    { title: '批号', dataIndex: 'batchNo', width: 110 },
    { title: '经销商', dataIndex: 'distributor', width: 140 },
    { title: '预期区域', dataIndex: 'expectedRegion', width: 100 },
    { title: '实际客户', dataIndex: 'actualCustomer', width: 140 },
    { title: '实际区域', dataIndex: 'actualRegion', width: 100 },
    { title: '数量', dataIndex: 'quantity', width: 70 },
    { title: '日期', dataIndex: 'date', width: 100 },
  ];

  return (
    <div>
      <h2 style={{ marginBottom: 16 }}>🚨 串货预警</h2>
      <Space style={{ marginBottom: 16 }}>
        <Input placeholder="按批号筛选" onPressEnter={(e) => load((e.target as HTMLInputElement).value)}
          prefix={<SearchOutlined />} style={{ width: 200 }} allowClear />
      </Space>
      <Table rowKey={(r,i) => String(i)} columns={columns} dataSource={data} loading={loading} pagination={false} />
    </div>
  );
}
