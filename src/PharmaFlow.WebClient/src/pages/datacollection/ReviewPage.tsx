import { useState, useEffect, useCallback } from 'react';
import { Table, Button, Select, Space, Tag, message, Modal, Input } from 'antd';
import { CheckOutlined, CloseOutlined, ReloadOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { useAuthStore } from '../../store/authStore';
import * as dcApi from '../../api/datacollection';
import type { DistributionReviewDto } from '../../api/datacollection';

const STATUS_OPTIONS = [
  { label: '待审核', value: '待审核' },
  { label: '已通过', value: '已通过' },
  { label: '已驳回', value: '已驳回' },
];

export default function ReviewPage() {
  const canWrite = useAuthStore(s => s.canWrite);
  const [data, setData] = useState<DistributionReviewDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState('待审核');
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);
  const [rejectModalOpen, setRejectModalOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await dcApi.getDistributionReview({ page: 0, size: 99999, status: statusFilter });
      setData(res.items);
    } catch (e: any) { message.error(e?.message || '加载失败'); }
    finally { setLoading(false); }
  }, [statusFilter]);

  useEffect(() => { load(); }, [load]);

  const handleApprove = async () => {
    if (selectedRowKeys.length === 0) { message.warning('请选择记录'); return; }
    try {
      await dcApi.approveDistributionReview(selectedRowKeys as string[]);
      message.success(`已通过 ${selectedRowKeys.length} 条`);
      setSelectedRowKeys([]); load();
    } catch (e: any) { message.error(e?.message || '操作失败'); }
  };

  const handleReject = async () => {
    if (!rejectReason.trim()) { message.warning('请输入驳回原因'); return; }
    try {
      await dcApi.rejectDistributionReview(selectedRowKeys as string[], rejectReason);
      message.success(`已驳回 ${selectedRowKeys.length} 条`);
      setRejectModalOpen(false); setRejectReason(''); setSelectedRowKeys([]); load();
    } catch (e: any) { message.error(e?.message || '操作失败'); }
  };

  const columns: ColumnsType<DistributionReviewDto> = [
    { title: '药品', dataIndex: 'drugName', width: 180 },
    { title: '批号', dataIndex: 'batchNo', width: 100 },
    { title: '客户', dataIndex: 'toCustomerName', width: 180, ellipsis: true },
    { title: '数量', dataIndex: 'quantity', width: 70 },
    { title: '单价', dataIndex: 'taxedUnitPrice', width: 80 },
    { title: '日期', dataIndex: 'distributionDate', width: 110, render: (v: string) => v?.split('T')[0] },
    { title: '省份', dataIndex: 'province', width: 100, ellipsis: true },
    {
      title: '审核状态', dataIndex: 'reviewStatus', width: 80,
      render: (v: string) => {
        const m: Record<string, string> = { '待审核': 'warning', '已通过': 'success', '已驳回': 'error' };
        return <Tag color={m[v] || 'default'}>{v}</Tag>;
      },
    },
    { title: '驳回原因', dataIndex: 'rejectReason', width: 150, ellipsis: true },
  ];

  return (
    <div>
      <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
        <Space>
          <Select value={statusFilter} style={{ width: 120 }}
            onChange={(v) => setStatusFilter(v)} options={STATUS_OPTIONS} />
          <Button icon={<ReloadOutlined />} onClick={load}>刷新</Button>
        </Space>
        {canWrite && statusFilter === '待审核' && (
          <Space>
            <Button type="primary" icon={<CheckOutlined />} onClick={handleApprove}
              disabled={selectedRowKeys.length === 0}>通过</Button>
            <Button danger icon={<CloseOutlined />} onClick={() => setRejectModalOpen(true)}
              disabled={selectedRowKeys.length === 0}>驳回</Button>
          </Space>
        )}
      </div>

      <Table columns={columns} dataSource={data} rowKey="id" loading={loading}
        rowSelection={{ selectedRowKeys, onChange: (keys) => setSelectedRowKeys(keys) }}
        scroll={{ x: 1000 }} pagination={{ defaultPageSize: 20, showTotal: (t) => `共 ${t} 条`, showSizeChanger: true }} />

      <Modal title="驳回" open={rejectModalOpen} onOk={handleReject}
        onCancel={() => { setRejectModalOpen(false); setRejectReason(''); }}>
        <Input.TextArea rows={3} value={rejectReason} onChange={(e) => setRejectReason(e.target.value)}
          placeholder="请输入驳回原因" />
      </Modal>
    </div>
  );
}