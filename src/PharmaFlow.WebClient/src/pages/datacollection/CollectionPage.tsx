import { useState, useEffect, useCallback } from 'react';
import { Table, Button, Modal, Form, Input, Select, Switch, Space, Tabs, Tag, message, Popconfirm } from 'antd';
import { PlusOutlined, ReloadOutlined, ThunderboltOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { useAuthStore } from '../../store/authStore';
import * as dcApi from '../../api/datacollection';
import * as ruleApi from '../../api/importrules';
import type { CollectionSourceDto, CollectionTaskDto, CollectionRecordDto } from '../../types';

const COLUMN_NAMES: Record<string, string> = {
  FSELLDATE: '日期', FORDERNUMBER: '单据编号', FBRANCHORGNAME: '公司名称',
  FCUSTOMERCODE: '销往单位编码', FCUSTOMERNAME: '销往单位', FPRODUCTCODE: '药品编码',
  FPRODUCTNAME: '药品名称', FSPECIFICATION: '规格', FQUANTITY: '数量', FPRICE: '单价',
  FBATCHNUMBER: '批号', FBILLER: '开票员', FFACTORY: '生产厂家', FAREA: '区域',
  FUNIT: '包装', IONAME: '库存组织', FDEPARTMENT: '所属部门', FBUYER: '采购员',
  FVALIDITY: '有效期', BEIZHU: '备注', FBUSINESS_LICENSE_NO: '营业执照号',
  FSECONDCUSTCODE: '二级单位编号', FSECONDCUSTNAME: '二级单位名称',
  FORDERTYPE: '网上订单类型', FCUSTBUSINESSTYPE: '客户类型', GROUPSELLID: '销售记录ID',
  FBRANCHORGCODE: '分支机构编码', FNEWBRANCHORGNAME: '新分支机构名称',
};

const SUMMARY_COLUMNS = [
  { key: 'DataType', title: '数据类型', width: 90, isSelect: true, options: ['采购流向', '销售流向'] },
  { key: 'DrugName', title: '药品名称', width: 150 },
  { key: 'Specification', title: '规格', width: 120 },
  { key: 'BatchNo', title: '批号', width: 100 },
  { key: 'CompanyName', title: '来源', width: 180 },
  { key: 'ToCustomerName', title: '去向', width: 180 },
  { key: 'TaxedUnitPrice', title: '单价', width: 80 },
  { key: 'Quantity', title: '数量', width: 80 },
  { key: 'DistributionDate', title: '日期', width: 110 },
  { key: 'Province', title: '区域', width: 150 },
  { key: 'ExpiryDate', title: '有效期', width: 110 },
] as const;

export default function CollectionPage() {
  const canWrite = useAuthStore(s => s.canWrite);
  const [activeTab, setActiveTab] = useState('sources');
  return (
    <div>
      <Tabs activeKey={activeTab} onChange={setActiveTab} items={[
        { key: 'sources', label: '采集源配置', children: <SourceTab canWrite={canWrite} /> },
        { key: 'datamanage', label: '采集数据管理', children: <DataManageTab /> },
        { key: 'summary', label: '采集汇总', children: <SummaryTab /> },
      ]} />
    </div>
  );
}

// ═══════════════════════════════════════
// Tab 1: 采集源配置
// ═══════════════════════════════════════
function SourceTab({ canWrite }: { canWrite: boolean }) {
  const [data, setData] = useState<CollectionSourceDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<CollectionSourceDto | null>(null);
  const [form] = Form.useForm();
  const [rules, setRules] = useState<{ id: string; name: string }[]>([]);
  const [logModal, setLogModal] = useState(false);
  const [logSourceId, setLogSourceId] = useState('');

  const loadRules = async () => { try { const r = await ruleApi.getRules({ page: 0, size: 99999 }); setRules(r.items); } catch { } };
  const load = useCallback(async () => {
    setLoading(true);
    try { const r = await dcApi.getSources({ page: 0, size: 99999 }); setData(r.items); }
    catch (e: any) { message.error(e?.message || '加载失败'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { load(); loadRules(); }, [load]);

  const applyHelper = () => {
    const v = form.getFieldsValue();
    if (!v.authConfig || v.authConfig === '{}') {
      form.setFieldsValue({ authConfig: JSON.stringify({ headers: { 'User-Agent': 'Mozilla/5.0' } }, null, 2) });
    }
    if (!v.exportConfig || v.exportConfig === '{}') {
      form.setFieldsValue({ exportConfig: JSON.stringify({ export: { url: (v.baseUrl || ''), method: 'GET', dataFormat: 'json' } }, null, 2) });
    }
    if (!v.transformConfig || v.transformConfig === '{}') {
      form.setFieldsValue({ transformConfig: JSON.stringify({ targetEntity: 'DistributionRecord', fieldMappings: {} }, null, 2) });
    }
    message.success('已生成默认模板，请在下方编辑');
  };

  const handleSave = async () => {
    try {
      const v = await form.validateFields();
      // JSON 文本框为权威来源，直接使用
      if (!v.transformConfig) v.transformConfig = editing?.transformConfig || '{"targetEntity":"DistributionRecord","fieldMappings":{}}';
      if (editing) { await dcApi.updateSource(editing.id, v); message.success('更新成功'); }
      else { await dcApi.createSource(v); message.success('创建成功'); }
      setModalOpen(false); load();
    } catch (e: any) { if (e?.message && !e.errorFields) message.error(e.message); }
  };

  const fillFormFromSource = (src: CollectionSourceDto) => {
    form.setFieldsValue(src);
  };

  const handleTrigger = async (id: string) => {
    try { const t = await dcApi.triggerCollection(id); message.success(`采集完成，${t.totalRecords} 条`); load(); }
    catch (e: any) { message.error(e?.message || '采集失败'); }
  };

  const columns: ColumnsType<CollectionSourceDto> = [
    { title: '名称', dataIndex: 'name', width: 150 },
    { title: '平台', dataIndex: 'platformType', width: 80 },
    { title: 'URL', dataIndex: 'baseUrl', width: 180, ellipsis: true },
    { title: '状态', dataIndex: 'isEnabled', width: 60, render: (v: boolean) => <Tag color={v ? 'green' : 'default'}>{v ? '启用' : '停用'}</Tag> },
    ...(canWrite ? [{ title: '操作', width: 220, render: (_: any, r: CollectionSourceDto) => (
      <Space>
        <Button size="small" icon={<ThunderboltOutlined />} onClick={() => handleTrigger(r.id)}>采集</Button>
        <Button size="small" onClick={() => { setEditing(r); fillFormFromSource(r); setModalOpen(true); }}>编辑</Button>
        <Button size="small" onClick={() => { setLogSourceId(r.id); setLogModal(true); }}>日志</Button>
        <Popconfirm title="删除？" onConfirm={async () => { await dcApi.deleteSource(r.id); load(); }}>
          <Button size="small" danger>删除</Button>
        </Popconfirm>
      </Space>
    ) }] : []),
  ];

  return (
    <div>
      <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between' }}>
        <Button icon={<ReloadOutlined />} onClick={load}>刷新</Button>
        {canWrite && <Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditing(null); form.resetFields(); form.setFieldsValue({ platformType: '流向平台', isEnabled: true }); setModalOpen(true); }}>新增采集源</Button>}
      </div>
      <Table columns={columns} dataSource={data} rowKey="id" loading={loading} scroll={{ x: 800 }}
        pagination={{ defaultPageSize: 10, showTotal: (t) => `共 ${t} 条`, showSizeChanger: true }} />
      <Modal title={editing ? '编辑采集源' : '新增采集源'} open={modalOpen} onOk={handleSave} onCancel={() => setModalOpen(false)} width={700}>
        <Form form={form} layout="vertical">
          <Form.Item name="name" label="名称" rules={[{ required: true }]}><Input /></Form.Item>
          <Space><Form.Item name="platformType" label="平台类型"><Select options={[{ label: '流向平台', value: '流向平台' }, { label: 'ERP系统', value: 'ERP系统' }, { label: '其他', value: '其他' }]} style={{ width: 130 }} /></Form.Item>
            <Form.Item name="isEnabled" label="启用" valuePropName="checked"><Switch /></Form.Item></Space>
          <Form.Item name="baseUrl" label="平台URL" rules={[{ required: true }]}><Input placeholder="http://example.com" /></Form.Item>
          <Form.Item name="scheduleCron" label="定时Cron" tooltip="留空仅手动"><Input placeholder="0 9 * * *" /></Form.Item>
          <Form.Item name="ruleId" label="导入规则"><Select allowClear placeholder="可选" options={rules.map(r => ({ label: r.name, value: r.id }))} /></Form.Item>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
            <strong>认证配置 (JSON)</strong>
            <Button size="small" onClick={applyHelper}>生成默认模板</Button>
          </div>
          <Form.Item name="authConfig" rules={[{ required: true }]}><Input.TextArea rows={6} placeholder='{"token":{"cookie_name":"FLOW_TOKEN","value":"..."}}' /></Form.Item>
          <strong>导出配置 (JSON)</strong>
          <Form.Item name="exportConfig" rules={[{ required: true }]}><Input.TextArea rows={8} placeholder='{"export":{"url":"http://...","method":"POST","dataFormat":"json"}}' /></Form.Item>
          <div style={{ color: '#888', fontSize: 12, marginBottom: 8 }}>
            字段映射+清洗请在「采集数据管理 → 配置映射」中设置
          </div>
        </Form>
      </Modal>
      <SourceLogModal sourceId={logSourceId} open={logModal} onClose={() => setLogModal(false)} />
    </div>
  );
}

// ── 日志弹窗 ──
function SourceLogModal({ sourceId, open, onClose }: { sourceId: string; open: boolean; onClose: () => void }) {
  const [tasks, setTasks] = useState<CollectionTaskDto[]>([]);
  const [loading, setLoading] = useState(false);
  useEffect(() => { if (!open || !sourceId) return; (async () => { setLoading(true); try { const r = await dcApi.getTasks({ page: 0, size: 99999, sourceId }); setTasks(r.items); } catch { } finally { setLoading(false); } })(); }, [open, sourceId]);
  const columns: ColumnsType<CollectionTaskDto> = [
    { title: '#', width: 40, render: (_: any, __: any, i: number) => i + 1 },
    { title: '开始时间', dataIndex: 'startedAt', width: 160, render: (v: string) => v ? new Date(v).toLocaleString() : '-' },
    { title: '完成时间', dataIndex: 'completedAt', width: 160, render: (v: string) => v ? new Date(v).toLocaleString() : '-' },
    { title: '状态', dataIndex: 'status', width: 70, render: (v: string) => { const m: Record<string, string> = { '已完成': 'success', '失败': 'error', '执行中': 'processing' }; return <Tag color={m[v] || 'default'}>{v}</Tag>; } },
    { title: '采集数', dataIndex: 'totalRecords', width: 70 },
    { title: '成功', dataIndex: 'successCount', width: 60 },
    { title: '重复', dataIndex: 'skippedCount', width: 60, render: (v: number) => v > 0 ? <span style={{ color: '#faad14' }}>{v}</span> : v },
    { title: '错误信息', dataIndex: 'errorMessage', width: 200, ellipsis: true },
  ];
  return (
    <Modal title="采集日志" open={open} onCancel={onClose} footer={null} width={900}>
      <Table columns={columns} dataSource={tasks} rowKey="id" loading={loading} size="small"
        pagination={{ defaultPageSize: 10, showTotal: (t) => `共 ${tasks.length} 次采集` }} />
    </Modal>
  );
}

// ═══════════════════════════════════════
// Tab 2: 采集数据管理
// ═══════════════════════════════════════
function DataManageTab() {
  const [tasks, setTasks] = useState<CollectionTaskDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(false);
  const [viewModal, setViewModal] = useState(false);
  const [mapModal, setMapModal] = useState(false);
  const [selectedTask, setSelectedTask] = useState<CollectionTaskDto | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await dcApi.getTasks({ page: 0, size: 99999 });
      setTasks(res.items);
      setAutoRefresh(res.items.some(t => t.status === '执行中' || t.status === '待执行'));
    } catch (e: any) { message.error(e?.message || '加载失败'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { load(); }, [load]);
  useEffect(() => { if (!autoRefresh) return; const t = setInterval(load, 5000); return () => clearInterval(t); }, [autoRefresh, load]);

  const groupedMap = new Map<string, { sourceName: string; sourceId: string; totalRecords: number; successCount: number; skipped: number; lastTaskId: string; lastTime: string; anySubmitted: boolean; runCount: number }>();
  tasks.forEach(t => {
    const g = groupedMap.get(t.sourceId) || { sourceName: t.sourceName, sourceId: t.sourceId, totalRecords: 0, successCount: 0, skipped: 0, lastTaskId: '', lastTime: '', anySubmitted: false, runCount: 0 };
    g.totalRecords += t.totalRecords; g.successCount += t.successCount; g.skipped += t.skippedCount; g.runCount++;
    if (t.isSubmitted) g.anySubmitted = true;
    if (!g.lastTime || (t.startedAt && t.startedAt > g.lastTime)) { g.lastTime = t.startedAt || ''; g.lastTaskId = t.id; }
    groupedMap.set(t.sourceId, g);
  });
  const mergedTasks = Array.from(groupedMap.values());

  const columns: ColumnsType<any> = [
    { title: '采集源', dataIndex: 'sourceName', width: 120 },
    { title: '采集次数', dataIndex: 'runCount', width: 70 },
    { title: '总记录', dataIndex: 'totalRecords', width: 60 },
    { title: '成功', dataIndex: 'successCount', width: 60, render: (v: number) => <span style={{ color: '#52c41a' }}>{v}</span> },
    { title: '重复', dataIndex: 'skipped', width: 60, render: (v: number) => v > 0 ? <span style={{ color: '#faad14' }}>{v}</span> : v },
    { title: '最近采集', dataIndex: 'lastTime', width: 150, render: (v: string) => v ? new Date(v).toLocaleString() : '-' },
    { title: '已提交', dataIndex: 'anySubmitted', width: 70, render: (v: boolean) => v ? <Tag color="blue">是</Tag> : <Tag>否</Tag> },
    { title: '操作', width: 200, render: (_: any, r: any) => (
      <Space>
        <Button size="small" onClick={() => { setSelectedTask({ id: '', sourceId: r.sourceId, sourceName: r.sourceName } as any); setViewModal(true); }}>查看数据</Button>
        <Button size="small" onClick={async () => { const res = await dcApi.getTasks({ page: 0, size: 1, sourceId: r.sourceId }); if (res.items[0]) { setSelectedTask(res.items[0]); setMapModal(true); } }}>配置映射</Button>
        {!r.anySubmitted && <Button size="small" type="primary" onClick={async () => { const res = await dcApi.getTasks({ page: 0, size: 99999, sourceId: r.sourceId }); for (const t of res.items) { await dcApi.submitTask(t.id); } load(); message.success('已提交'); }}>提交</Button>}
        <Popconfirm title="删除此源所有数据？" onConfirm={async () => { const res = await dcApi.getTasks({ page: 0, size: 99999, sourceId: r.sourceId }); for (const t of res.items) { await dcApi.deleteTask(t.id); } load(); message.success('已删除'); }}>
          <Button size="small" danger>删除</Button>
        </Popconfirm>
      </Space>
    ) },
  ];

  return (
    <div>
      <div style={{ marginBottom: 16 }}><Button icon={<ReloadOutlined />} onClick={load}>刷新</Button>
        {autoRefresh && <Tag color="processing" style={{ marginLeft: 8 }}>自动刷新</Tag>}</div>
      <Table columns={columns} dataSource={mergedTasks} rowKey="sourceId" loading={loading} scroll={{ x: 1000 }}
        pagination={{ defaultPageSize: 10, showTotal: (t) => `共 ${t} 条`, showSizeChanger: true }} />
      <Modal title={`数据 - ${selectedTask?.sourceName}`} open={viewModal} onCancel={() => setViewModal(false)} width={1200} footer={null}>
        <TaskDataView taskId={selectedTask?.id || undefined} sourceId={(selectedTask as any)?.sourceId} />
      </Modal>
      <Modal title={`映射规则 - ${selectedTask?.sourceName}`} open={mapModal} onCancel={() => setMapModal(false)} width={800} footer={null}>
        <TaskMapView taskId={selectedTask?.id} sourceName={selectedTask?.sourceName || ''} sourceId={selectedTask?.sourceId || ''} onSaved={() => setMapModal(false)} />
      </Modal>
    </div>
  );
}

function TaskDataView({ taskId, sourceId }: { taskId?: string; sourceId?: string }) {
  const [data, setData] = useState<CollectionRecordDto[]>([]);
  const [loading, setLoading] = useState(false);
  useEffect(() => {
    if (!taskId && !sourceId) return;
    (async () => {
      setLoading(true);
      try {
        const params: any = { page: 0, size: 99999 };
        if (taskId) params.taskId = taskId;
        else params.sourceId = sourceId;
        const r = await dcApi.getRecords(params);
        setData(r.items);
      } catch { } finally { setLoading(false); }
    })();
  }, [taskId, sourceId]);
  const rows = data.map(r => { try { return { ...r, _p: JSON.parse(r.rawDataJson) }; } catch { return { ...r, _p: {} }; } });
  const keys = new Set<string>(); rows.forEach(r => Object.keys(r._p).forEach(k => keys.add(k)));
  return <Table columns={[{ title: '状态', dataIndex: 'status', width: 60, fixed: 'left' as const, render: (v: string) => <Tag>{v}</Tag> },
    ...Array.from(keys).map(k => ({ title: COLUMN_NAMES[k] || k, width: 120, ellipsis: true as const, render: (_: any, r: any) => { const v = r._p?.[k]; return v != null ? String(v) : ''; } })),
  ]} dataSource={rows} rowKey="id" loading={loading} scroll={{ x: keys.size * 120 }} size="small"
    pagination={{ defaultPageSize: 20, showTotal: (t: number) => `${t} 条`, showSizeChanger: true }} />;
}

function TaskMapView({ taskId, sourceName, sourceId, onSaved }: { taskId?: string; sourceName: string; sourceId: string; onSaved: () => void }) {
  const [sourceFields, setSourceFields] = useState<string[]>([]);
  const [mappings, setMappings] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const canWrite = useAuthStore(s => s.canWrite);
  useEffect(() => { if (!taskId) return; (async () => { try { const r = await dcApi.getRecords({ page: 0, size: 5, taskId }); const keys = new Set<string>(); r.items.forEach(x => { try { Object.keys(JSON.parse(x.rawDataJson)).forEach(k => keys.add(k)); } catch { } }); setSourceFields(Array.from(keys).sort()); const s = await dcApi.getSourceById(sourceId); if (s?.transformConfig) { try { const tc = JSON.parse(s.transformConfig); const ex: Record<string, string> = {}; if (tc.fieldMappings) Object.entries(tc.fieldMappings).forEach(([t, c]: [string, any]) => { ex[t] = c.source || t; }); if (tc.sourceType) ex['DataType'] = tc.sourceType; setMappings(ex); } catch { } } } catch { } })(); }, [taskId]);
  const save = async () => { setSaving(true); try { const fm: Record<string, any> = {}; Object.entries(mappings).forEach(([t, s]) => { if (s && t !== 'DataType') fm[t] = { source: s }; }); const s = await dcApi.getSourceById(sourceId); let tc: any = {}; try { tc = JSON.parse(s?.transformConfig || '{}'); } catch { } tc = { ...tc, fieldMappings: fm, sourceType: mappings['DataType'] || '' }; await dcApi.updateSource(sourceId, { ...s, transformConfig: JSON.stringify(tc) }); message.success('保存成功'); onSaved(); } catch (e: any) { message.error(e?.message || '失败'); } finally { setSaving(false); } };
  return <div>
    <div style={{ marginBottom: 12, display: 'flex', gap: 12, alignItems: 'center' }}><span>采集源：</span><Tag color="blue">{sourceName}</Tag>
      {canWrite && <Button type="primary" size="small" loading={saving} onClick={save}>保存映射</Button>}</div>
    <Table size="small" dataSource={[
      { key: '_source', title: '采集源', source: sourceName || '-', readonly: true },
      ...SUMMARY_COLUMNS.map(c => ({ key: c.key, title: c.title, source: mappings[c.key] || '', isSelect: (c as any).isSelect, options: (c as any).options, readonly: false })),
    ]} rowKey="key" pagination={false}
      columns={[{ title: '汇总列', dataIndex: 'title', width: 80 }, { title: '映射值', dataIndex: 'source', width: 250,
        render: (v: string, r: any) => r.readonly
          ? <Tag color="blue">{v}</Tag>
          : r.isSelect
          ? <Select value={v || undefined} placeholder="选择" style={{ width: 220 }} onChange={(val) => setMappings(p => ({ ...p, [r.key]: val || '' }))} options={(r.options || []).map((o: string) => ({ label: o, value: o }))} />
          : <Select value={v || undefined} allowClear placeholder="选择源字段" style={{ width: 220 }} onChange={(val) => setMappings(p => ({ ...p, [r.key]: val || '' }))} options={sourceFields.map(f => ({ label: `${COLUMN_NAMES[f] ? COLUMN_NAMES[f] + ' ' : ''}(${f})`, value: f }))} />,
      }]} />
  </div>;
}

// ═══════════════════════════════════════
// Tab 3: 采集汇总
// ═══════════════════════════════════════
function SummaryTab() {
  const [data, setData] = useState<CollectionRecordDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [sources, setSources] = useState<CollectionSourceDto[]>([]);
  const [sourceFilter, setSourceFilter] = useState<string | undefined>();

  useEffect(() => { (async () => { try { const r = await dcApi.getSources({ page: 0, size: 99999 }); setSources(r.items); } catch { } })(); }, []);
  const load = useCallback(async () => { setLoading(true); try { const [records, tasks] = await Promise.all([dcApi.getRecords({ page: 0, size: 99999, sourceId: sourceFilter }), dcApi.getTasks({ page: 0, size: 99999 })]); const submitted = new Set(tasks.items.filter(t => t.isSubmitted).map(t => t.sourceId)); const filtered = records.items.filter(x => submitted.has(x.sourceId)); const seen = new Set<string>(); const deduped = filtered.filter(x => seen.has(x.rawDataJson) ? false : (seen.add(x.rawDataJson), true)); setData(deduped); } catch { } finally { setLoading(false); } }, [sourceFilter]);
  useEffect(() => { load(); }, [load]);

  const sourceTypeMap: Record<string, string> = {};
  sources.forEach(s => { try { const tc = JSON.parse(s.transformConfig || '{}'); if (tc.sourceType) sourceTypeMap[s.id] = tc.sourceType; } catch { } });

  const rows = data.map(r => { try { const m = JSON.parse(r.cleanedDataJson || '{}'); m.DataType = sourceTypeMap[r.sourceId] || ''; return { ...r, _m: m }; } catch { return { ...r, _m: { DataType: sourceTypeMap[r.sourceId] || '' } }; } });

  const columns: ColumnsType<any> = [
    { title: '采集源', dataIndex: 'sourceName', width: 120, fixed: 'left' as const, render: (v: string) => <Tag color="blue">{v}</Tag> },
    ...SUMMARY_COLUMNS.map(c => ({ title: c.title, width: c.width, ellipsis: true as const,
      render: (_: any, r: any) => { const v = r._m?.[c.key]; if (v == null || v === '') return ''; const s = String(v); return (c.key === 'DistributionDate' || c.key === 'ExpiryDate') ? (s.includes('T') ? s.split('T')[0] : s) : s; },
    })),
  ];

  return <div>
    <div style={{ marginBottom: 16, display: 'flex', gap: 12, alignItems: 'center' }}>
      <Select value={sourceFilter} allowClear placeholder="按采集源筛选" style={{ width: 200 }} onChange={(v) => setSourceFilter(v)} options={sources.map(s => ({ label: s.name, value: s.id }))} />
      <Button icon={<ReloadOutlined />} onClick={load}>刷新</Button>
      <Popconfirm title="清空后数据从汇总移除，可在采集数据管理中重新提交" onConfirm={async () => { await dcApi.clearSummary(); load(); message.success('已清空'); }}>
        <Button danger>清空</Button>
      </Popconfirm>
      <Tag>{data.length} 条</Tag></div>
    <Table columns={columns} dataSource={rows} rowKey="id" loading={loading} scroll={{ x: 1600 }} size="small"
      pagination={{ defaultPageSize: 20, showTotal: (t: number) => `共 ${t} 条`, showSizeChanger: true }} />
  </div>;
}