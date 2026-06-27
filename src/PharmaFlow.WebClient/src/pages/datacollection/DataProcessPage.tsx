import { useState, useEffect, useCallback } from 'react';
import { Table, Button, Select, Tag, message, Modal, Descriptions, Space, Spin, Tabs } from 'antd';
import { PlayCircleOutlined, ExpandOutlined, ReloadOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { useAuthStore } from '../../store/authStore';
import * as dcApi from '../../api/datacollection';
import * as ruleApi from '../../api/importrules';
import type { CollectionTaskDto } from '../../types';
import type { BatchPreview } from '../../api/datacollection';
import type { ImportRuleDto } from '../../api/importrules';

export default function DataProcessPage() {
  const canWrite = useAuthStore(s => s.canWrite);
  const [tasks, setTasks] = useState<CollectionTaskDto[]>([]);
  const [rules, setRules] = useState<ImportRuleDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedTask, setSelectedTask] = useState<string | undefined>();
  const [selectedRule, setSelectedRule] = useState<string | undefined>();
  const [previewModalOpen, setPreviewModalOpen] = useState(false);
  const [previewData, setPreviewData] = useState<BatchPreview | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [importing, setImporting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [taskRes, ruleRes] = await Promise.all([
        dcApi.getTasks({ page: 0, size: 99999 }),
        ruleApi.getRules({ page: 0, size: 99999 }),
      ]);
      setTasks(taskRes.items.filter(t => t.status === '已完成' && t.totalRecords > 0));
      setRules(ruleRes.items);
    } catch (e: any) { message.error(e?.message || '加载失败'); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { load(); }, [load]);

  const handlePreview = async () => {
    if (!selectedTask) { message.warning('请选择采集任务'); return; }
    setPreviewLoading(true); setPreviewModalOpen(true);
    try {
      const data = await dcApi.previewBatch(selectedTask);
      setPreviewData(data);
    } catch (e: any) { message.error(e?.message || '预览失败'); setPreviewModalOpen(false); }
    finally { setPreviewLoading(false); }
  };

  const handleImport = async () => {
    if (!selectedTask) return;
    setImporting(true);
    try {
      const result = await dcApi.importBatch(selectedTask);
      message.success(`导入完成：创建药品 ${result.createdDrugs} ，客户 ${result.createdCustomers} ，导入 ${result.imported} 条`);
      setPreviewModalOpen(false); load();
    } catch (e: any) { message.error(e?.response?.data?.message || e?.message || '导入失败'); }
    finally { setImporting(false); }
  };

  const columns: ColumnsType<CollectionTaskDto> = [
    { title: '采集源', dataIndex: 'sourceName', width: 150 },
    { title: '记录数', dataIndex: 'totalRecords', width: 80 },
    { title: '时间', dataIndex: 'startedAt', width: 160, render: (v: string) => v ? new Date(v).toLocaleString() : '-' },
    { title: '状态', dataIndex: 'status', width: 80, render: (v: string) => <Tag color="success">{v}</Tag> },
  ];

  return (
    <div>
      <div style={{ marginBottom: 16, display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <Select value={selectedTask} onChange={setSelectedTask} allowClear placeholder="选择采集任务"
          style={{ width: 300 }} options={tasks.map(t => ({ label: `${t.sourceName} (${t.totalRecords}条)`, value: t.id }))} />
        <Select value={selectedRule} onChange={setSelectedRule} allowClear placeholder="选择导入规则（可选）"
          style={{ width: 250 }} options={rules.map(r => ({ label: r.name, value: r.id }))} />
        {canWrite && <Button type="primary" icon={<PlayCircleOutlined />} onClick={handlePreview} disabled={!selectedTask}>处理</Button>}
        <Button icon={<ReloadOutlined />} onClick={load}>刷新</Button>
      </div>

      <Table columns={columns} dataSource={tasks} rowKey="id" loading={loading}
        scroll={{ x: 500 }} pagination={{ defaultPageSize: 10, showTotal: (t) => `共 ${t} 个批次`, showSizeChanger: true }} />

      <Modal title="处理预览" open={previewModalOpen} onCancel={() => setPreviewModalOpen(false)} width={900}
        footer={<Space><Button onClick={() => setPreviewModalOpen(false)}>取消</Button>
          {canWrite && previewData && <Button type="primary" loading={importing} onClick={handleImport}>
            确认导入 ({previewData.totalRecords} 条，导入后需审核)</Button>}
        </Space>}>
        {previewLoading ? <div style={{ textAlign: 'center', padding: 40 }}><Spin size="large" /></div> :
          previewData ? <div>
            <Descriptions bordered size="small" column={3}>
              <Descriptions.Item label="总记录">{previewData.totalRecords}</Descriptions.Item>
              <Descriptions.Item label="药品">{previewData.drugs.length}（新增 {previewData.drugs.filter(d => !d.matched).length}）</Descriptions.Item>
              <Descriptions.Item label="客户">{previewData.customers.length}（新增 {previewData.customers.filter(c => !c.matched).length}）</Descriptions.Item>
            </Descriptions>
            <h4 style={{ marginTop: 12 }}>药品匹配</h4>
            <Table size="small" dataSource={previewData.drugs} rowKey="name" pagination={false}
              columns={[
                { title: '药品', dataIndex: 'name', width: 180 },
                { title: '原始规格', dataIndex: 'originalSpec', width: 120 },
                { title: '规范化', dataIndex: 'normalizedSpec', width: 120, render: (v: string, r: any) => v !== r.originalSpec ? <span style={{ color: '#1890ff' }}>{v}</span> : v },
                { title: '匹配', dataIndex: 'matched', width: 70, render: (v: boolean) => v ? <Tag color="green">已匹配</Tag> : <Tag color="orange">新增</Tag> },
              ]} />
            <h4 style={{ marginTop: 12 }}>客户匹配</h4>
            <Table size="small" dataSource={previewData.customers} rowKey="name" pagination={false}
              columns={[
                { title: '客户', dataIndex: 'name', width: 200 },
                { title: '编码', dataIndex: 'code', width: 120 },
                { title: '匹配', dataIndex: 'matched', width: 70, render: (v: boolean) => v ? <Tag color="green">已匹配</Tag> : <Tag color="orange">新增</Tag> },
              ]} />
          </div> : null}
      </Modal>
    </div>
  );
}