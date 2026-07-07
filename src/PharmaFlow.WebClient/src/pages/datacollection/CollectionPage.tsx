import { useState, useEffect, useCallback } from 'react';
import { useLocation } from 'react-router-dom';
import { Table, Button, Modal, Form, Input, Select, Switch, Space, Tag, message, Popconfirm, DatePicker } from 'antd';
import { CheckCircleOutlined, DeleteOutlined, EditOutlined, EyeOutlined, FileTextOutlined, KeyOutlined, PlusOutlined, ReloadOutlined, RollbackOutlined, ThunderboltOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { useAuthStore } from '../../store/authStore';
import * as dcApi from '../../api/datacollection';
import RowActions from '../../components/RowActions';

import type { CollectionSourceDto, CollectionTaskDto, CollectionRecordDto, CollectionSummaryRecordDto, DropdownItem } from '../../types';

const COLUMN_NAMES: Record<string, string> = {
  // 九州通 F字段
  FSELLDATE: '日期', FORDERNUMBER: '单据编号', FBRANCHORGNAME: '公司名称',
  FCUSTOMERCODE: '销往单位编码', FCUSTOMERNAME: '销往单位', FPRODUCTCODE: '药品编码',
  FPRODUCTNAME: '药品名称', FSPECIFICATION: '规格', FQUANTITY: '数量', FPRICE: '单价',
  FBATCHNUMBER: '批号', FBILLER: '开票员', FFACTORY: '生产厂家', FAREA: '区域',
  FUNIT: '包装单位', IONAME: '库存组织', FDEPARTMENT: '所属部门', FBUYER: '采购员',
  FVALIDITY: '有效期', BEIZHU: '备注', FBUSINESS_LICENSE_NO: '营业执照号',
  FSECONDCUSTCODE: '二级单位编号', FSECONDCUSTNAME: '二级单位名称',
  FORDERTYPE: '网上订单类型', FCUSTBUSINESSTYPE: '客户类型', GROUPSELLID: '销售记录ID',
  FBRANCHORGCODE: '分支机构编码', FNEWBRANCHORGNAME: '新分支机构名称',
  FDZSW: '电商', CREATETIME: '创建时间', LASTMODIFYTIME: '最后修改时间',
  FNEWBRANCHORGCODE: '新分支机构编码', FCUSTABBREVIATION: '客户简称',
  FPRICE: '单价', FACTORY_SHORT: '生产厂家简称', FPACKAGE: '包装',
  FCLEARING_AMT: '结算金额', IOID: '内部组织ID', FCUSTIDENTIFY: '单位识别',
  FBRANCHORGCODE: '分支机构编码', FPARENTID: '上级编码', FAmount: '金额',
  // 山东九州通
  biaoshi: '标识', branchid: '公司', djbh: '单据编号', rq: '日期',
  danwbh: '单位编号', dwmch: '客户名称', spbh: '药品编码', spmch: '药品名称',
  shpgg: '规格', shengccj: '生产厂家', quyufl: '区域分类', cityname: '城市',
  jlgg: '计量规格', dw: '单位', pihao: '批号', sxrq: '有效期',
  shl: '数量', dj: '单价', cbdj: '成本单价', je: '金额',
  dzsw: '电商', dirsalestaffname: '销售经理', businessman: '业务员',
  ouname: '部门', usagename: '用途', ioname: '库存组织',
  twocustname: '二级客户', FPARENTID: '上级编码',
  // 通用
  pageNum: '页码', isAsc: '排序', beginTime: '开始时间', endTime: '结束时间',
  custno: '客户编号', custname: '客户名称', xsqy: '销售区域', khlx: '客户类型',
  prodno: '产品编号', prodname: '产品名称', lotno: '批号',
  total: '总数', code: '编码', msg: '消息', rows: '行数据',
  EJDWBH: '二级单位编号', EJDWMC: '二级单位名称', SUPPLIER_NOTES: '供应商备注',
  // 国药控股广西
  compname: '公司名称', billno: '单据号', makedate: '开单日期',
  goodsname: '商品名称', goodscode: '商品编码', cstname: '客户名称',
  cstcode: '客户编码', lotqty: '销售数量', prc: '含税单价',
  sumvalue: '含税金额', ghsname: '批次供应商', msunitno: '单位',
  sellname: '销售代表', saledept: '销售部门', housename: '仓库',
  packnum: '包装数量', prddate: '生产日期', enddate: '失效日期',
  releasdate: '释放日期', wmsdate: '出库确认日期', invdate: '发票日期',
  ardate: '记账日期', flowname: '业务类型', stopflag: '状态标志',
  addressname: '收货地址', bthdesc: '批次描述', spec: '规格',
  producer: '生产厂家', lotno: '批号', price: '单价', value: '金额',
  batchid: '批次ID', billid: '单据ID', buyerid: '采购员ID', compid: '公司ID',
  cstid: '客户ID', flowid: '流水ID', goodcstid: '商品客户ID', goodid: '商品ID',
  goodlinkid: '商品关联ID', id: '记录ID', opwms: 'WMS操作', ownerid: '归属ID',
  rn: '行号', status: '状态码', statusmark: '状态标记', tax: '税额', taxrate: '税率',
  // 江西PSI
  releasedateChar: '释放日期', grpno: '单号', goods: '商品编号',
  goodsName: '商品名称', qty: '数量', amt: '金额', endDateChar: '失效日期',
  ratifier: '批准文号', custCstcode: '客户编号', custDname: '客户名称',
  prodDname: '厂家全称', inprc: '进货价', ucstCodeName: '供应商',
  ulinkCodeName: '关联编码', regionName: '区域', cityName: '城市',
  ownername: '归属', chgtype: '类型', secondCustDname: '二级客户',
  custAddress: '地址', ubatchno: '批号2',
  // 河北国药乐仁堂
  ROWSN: '记录时间', rowsn: '记录时间',
  地市公司: '地市公司', 单据号: '单据号', 流向日期: '流向日期', 单据类型: '单据类型',
  供应商简称: '供应商简称', 客户编码: '客户编码', 客户名称: '客户名称', 客户简称: '客户简称',
  商品编码: '商品编码', 商品名称: '商品名称', 规格: '规格', 生产企业名称: '生产企业名称',
  计量单位: '计量单位', 批号: '批号', 生产日期: '生产日期', 有效期至: '有效期至',
  采购数量: '采购数量', 销售数量: '销售数量', 部门: '部门', 采购含税进价: '采购含税进价',
  原始供应商编码: '原始供应商编码', 原始供应商简称: '原始供应商简称',
  供应商联系人编码: '供应商联系人编码', 剂型: '剂型', 仓储部名称: '仓储部名称',
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

type PostRuleCondition = { field: string; operator: string; value?: string };
type UiPostRule = {
  name?: string;
  enabled: boolean;
  action: 'update' | 'exclude';
  excludeReason?: string;
  conditions: PostRuleCondition[];
  targetField: string;
  newValue: string;
};

const CONDITION_OPERATORS = [
  { label: '等于', value: 'equals' },
  { label: '包含', value: 'contains' },
  { label: '不等于', value: 'notEquals' },
  { label: '不包含', value: 'notContains' },
  { label: '为空', value: 'empty' },
  { label: '不为空', value: 'notEmpty' },
];

const createRule = (): UiPostRule => ({
  name: '',
  enabled: true,
  action: 'update',
  conditions: [{ field: 'DrugName', operator: 'equals', value: '' }],
  targetField: 'Specification',
  newValue: '',
});

const AUTH_CONFIG_PLACEHOLDER = JSON.stringify({
  loginPath: '/prod-api/login',
  loginMethod: 'POST',
  loginHeaders: {
    Accept: 'application/json, text/plain, */*',
    Origin: 'http://111.74.9.154:8097',
    Referer: 'http://111.74.9.154:8097/login?redirect=%2Findex',
    isToken: 'false',
  },
  preLogin: {
    url: '/prod-api/captchaImage',
    extract: [
      { name: 'uuid', type: 'json', path: 'uuid', required: true },
      { name: 'code', type: 'mathCaptcha', path: 'img', required: true },
    ],
  },
  passwordEncrypt: {
    type: 'rsa',
    name: 'password',
    value: 'your_plain_password',
    publicKey: 'MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A...',
  },
  loginBodyJson: '{"username":"...","password":"{{password}}","code":"{{code}}","uuid":"{{uuid}}"}',
  tokenExtract: {
    source: 'json',
    paths: ['token', 'data.token', 'access_token', 'data.access_token'],
    required: true,
    targets: [
      { type: 'header', name: 'Authorization', prefix: 'Bearer ' },
      { type: 'cookie', name: 'Psi-Syn-Token' },
    ],
  },
  headers: {
    Accept: 'application/json, text/plain, */*',
    Origin: 'http://111.74.9.154:8097',
    Referer: 'http://111.74.9.154:8097/psisyn/psisale',
  },
}, null, 2);

export default function CollectionPage() {
  const canWrite = useAuthStore(s => s.canWrite);
  const location = useLocation();
  const activeModule = location.pathname.endsWith('/data')
    ? 'datamanage'
    : location.pathname.endsWith('/summary')
      ? 'summary'
      : 'sources';

  return (
    <div>
      {activeModule === 'sources' && <SourceTab canWrite={canWrite} />}
      {activeModule === 'datamanage' && <DataManageTab />}
      {activeModule === 'summary' && <SummaryTab />}
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
  const [logModal, setLogModal] = useState(false);
  const [logSourceId, setLogSourceId] = useState('');
  const [tokenModal, setTokenModal] = useState(false);
  const [tokenSourceId, setTokenSourceId] = useState('');
  const [newToken, setNewToken] = useState('');
  const [scriptPath, setScriptPath] = useState('');
  const collectionMode = Form.useWatch('collectionMode', form);

  const load = useCallback(async () => {
    setLoading(true);
    try { const r = await dcApi.getSources({ page: 0, size: 99999 }); setData(r.items); }
    catch (e: any) { message.error(e?.message || '加载失败'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { load(); }, [load]);

  const applyHelper = () => {
    const v = form.getFieldsValue();
    if (v.collectionMode === 'PythonScript') {
      if (!v.exportConfig || v.exportConfig === '{}') {
        form.setFieldsValue({ exportConfig: JSON.stringify({ export: { pythonScript: '', dataFormat: 'json' } }, null, 2) });
      }
      if (!v.authConfig || v.authConfig === '{}') {
        form.setFieldsValue({ authConfig: '{}' });
      }
      message.success('请在脚本路径中填写 Python 脚本的完整路径');
      return;
    }
    if (!v.authConfig || v.authConfig === '{}') {
      form.setFieldsValue({ authConfig: AUTH_CONFIG_PLACEHOLDER });
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
      // Python脚本模式：构造 exportConfig 包含脚本路径
      if (v.collectionMode === 'PythonScript') {
        v.authConfig = '{}';
        v.exportConfig = JSON.stringify({ export: { pythonScript: scriptPath, dataFormat: 'json' } });
      }
      if (!v.transformConfig) v.transformConfig = editing?.transformConfig || '{"targetEntity":"DistributionRecord","fieldMappings":{}}';
      if (editing) { await dcApi.updateSource(editing.id, v); message.success('更新成功'); }
      else { await dcApi.createSource(v); message.success('创建成功'); }
      setModalOpen(false); load();
    } catch (e: any) { if (e?.message && !e.errorFields) message.error(e.message); }
  };

  const fillFormFromSource = (src: CollectionSourceDto) => {
    form.setFieldsValue(src);
    // Extract scriptPath from exportConfig for PythonScript mode
    try { const ec = JSON.parse(src.exportConfig || '{}'); setScriptPath(ec.export?.pythonScript || ec.pythonScript || ''); }
    catch { setScriptPath(''); }
  };

  const handleTrigger = async (id: string) => {
    try { const t = await dcApi.triggerCollection(id); message.success(`采集完成，${t.totalRecords} 条`); load(); }
    catch (e: any) { message.error(e?.message || '采集失败'); }
  };

  const columns: ColumnsType<CollectionSourceDto> = [
    { title: '名称', dataIndex: 'name', width: 150 },
    { title: '平台', dataIndex: 'platformType', width: 80 },
    { title: 'URL', dataIndex: 'baseUrl', width: 180, ellipsis: true },
    { title: '状态', dataIndex: 'isEnabled', width: 100, render: (v: boolean, r: CollectionSourceDto) => {
      if (r.lastCollectionStatus === 'Token已过期') return <Tag color="red">Token已过期</Tag>;
      return <Tag color={v ? 'green' : 'default'}>{v ? '启用' : '停用'}</Tag>;
    }},
    ...(canWrite ? [{ title: '操作', width: 250, render: (_: any, r: CollectionSourceDto) => (
      <RowActions actions={[
        { key: 'collect', label: '采集', icon: <ThunderboltOutlined />, onClick: () => handleTrigger(r.id) },
        { key: 'edit', label: '编辑', icon: <EditOutlined />, tone: 'primary', onClick: () => { setEditing(r); fillFormFromSource(r); setModalOpen(true); } },
        r.lastCollectionStatus === 'Token已过期' && { key: 'token', label: '更新Token', icon: <KeyOutlined />, tone: 'danger', onClick: () => { setTokenSourceId(r.id); setTokenModal(true); } },
        { key: 'log', label: '日志', icon: <FileTextOutlined />, onClick: () => { setLogSourceId(r.id); setLogModal(true); } },
        { key: 'delete', label: '删除', icon: <DeleteOutlined />, tone: 'danger', confirmTitle: '删除？', onClick: async () => { await dcApi.deleteSource(r.id); load(); } },
      ]} />
    ) }] : []),
  ];

  return (
    <div>
      <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between' }}>
        <Button icon={<ReloadOutlined />} onClick={load}>刷新</Button>
        {canWrite && <Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditing(null); form.resetFields(); form.setFieldsValue({ platformType: '流向平台', isEnabled: true, collectionMode: 'JsonConfig' }); setScriptPath(''); setModalOpen(true); }}>新增采集源</Button>}
      </div>
      <Table columns={columns} dataSource={data} rowKey="id" loading={loading} scroll={{ x: 800 }}
        pagination={{ defaultPageSize: 10, showTotal: (t) => `共 ${t} 条`, showSizeChanger: true }} />
      <Modal title={editing ? '编辑采集源' : '新增采集源'} open={modalOpen} onOk={handleSave} onCancel={() => setModalOpen(false)} width={700}>
        <Form form={form} layout="vertical">
          <Form.Item name="name" label="名称" rules={[{ required: true }]}><Input /></Form.Item>
          <Space><Form.Item name="platformType" label="平台类型"><Select options={[{ label: '流向平台', value: '流向平台' }, { label: 'ERP系统', value: 'ERP系统' }, { label: '其他', value: '其他' }]} style={{ width: 130 }} /></Form.Item>
            <Form.Item name="isEnabled" label="启用" valuePropName="checked"><Switch /></Form.Item></Space>
          <Form.Item name="baseUrl" label="平台URL" rules={[{ required: true }]}><Input placeholder="http://example.com" /></Form.Item>
          <Form.Item name="collectionMode" label="配置模式" rules={[{ required: true }]}>
            <Select options={[{ label: 'JSON配置', value: 'JsonConfig' }, { label: 'Python脚本', value: 'PythonScript' }]} style={{ width: 200 }}
              onChange={(val) => { if (val === 'PythonScript') { setScriptPath(''); form.setFieldsValue({ authConfig: '{}' }); } }} />
          </Form.Item>
          <Form.Item name="scheduleCron" label="定时Cron" tooltip="留空仅手动"><Input placeholder="0 9 * * *" /></Form.Item>

          {collectionMode !== 'PythonScript' && (
            <>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
                <strong>认证配置 (JSON)</strong>
                <Button size="small" onClick={applyHelper}>生成默认模板</Button>
              </div>
              <Form.Item name="authConfig" rules={[{ required: true }]}><Input.TextArea rows={8} placeholder={AUTH_CONFIG_PLACEHOLDER} /></Form.Item>
              <strong>导出配置 (JSON)</strong>
              <Form.Item name="exportConfig" rules={[{ required: true }]}><Input.TextArea rows={8} placeholder='{"export":{"url":"http://...","method":"POST","dataFormat":"json"}}' /></Form.Item>
            </>
          )}

          {collectionMode === 'PythonScript' && (
            <>
              <div style={{ marginBottom: 8 }}>
                <strong>Python 脚本配置</strong>
                <div style={{ color: '#888', fontSize: 12, marginBottom: 8 }}>
                  脚本需输出 JSON 数组到 stdout，认证和导出均由脚本接管。
                </div>
              </div>
              <Form.Item label="脚本路径" required>
                <Input value={scriptPath} onChange={e => setScriptPath(e.target.value)}
                  placeholder="e:\流向平台\PharmaFlow\scripts\qingdao_scraper.py" />
              </Form.Item>
              <Form.Item name="authConfig" hidden initialValue="{}"><Input /></Form.Item>
              <Form.Item name="exportConfig" hidden initialValue="{}"><Input.TextArea /></Form.Item>
            </>
          )}

          <div style={{ color: '#888', fontSize: 12, marginBottom: 8 }}>
            字段映射+清洗请在「采集数据管理 → 配置映射」中设置
          </div>
        </Form>
      </Modal>
      <SourceLogModal sourceId={logSourceId} open={logModal} onClose={() => setLogModal(false)} />
      <Modal title="更新Token" open={tokenModal} onCancel={() => setTokenModal(false)}
        onOk={async () => { if (!newToken.trim()) return; await dcApi.updateToken(tokenSourceId, newToken.trim()); message.success('Token已更新'); setTokenModal(false); setNewToken(''); load(); }}>
        <Input.TextArea rows={3} value={newToken} onChange={e => setNewToken(e.target.value)} placeholder="粘贴新的Token值" />
      </Modal>
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
      const [taskRes, srcRes] = await Promise.all([
        dcApi.getTasks({ page: 0, size: 99999 }),
        dcApi.getSources({ page: 0, size: 99999 }),
      ]);
      // 合并 autoSubmit 到 tasks
      const srcAutoSubmit: Record<string, boolean> = {};
      srcRes.items.forEach(s => { srcAutoSubmit[s.id] = s.autoSubmit; });
      const tasksWithFlag = taskRes.items.map(t => ({ ...t, autoSubmit: srcAutoSubmit[t.sourceId] || false }));
      setTasks(tasksWithFlag);
      setAutoRefresh(taskRes.items.some(t => t.status === '执行中' || t.status === '待执行'));
    } catch (e: any) { message.error(e?.message || '加载失败'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { load(); }, [load]);
  useEffect(() => { if (!autoRefresh) return; const t = setInterval(load, 5000); return () => clearInterval(t); }, [autoRefresh, load]);

  const groupedMap = new Map<string, { sourceName: string; sourceId: string; totalRecords: number; successCount: number; skipped: number; lastTaskId: string; lastTime: string; anySubmitted: boolean; runCount: number; autoSubmit: boolean }>();
  tasks.forEach(t => {
    const g = groupedMap.get(t.sourceId) || { sourceName: t.sourceName, sourceId: t.sourceId, totalRecords: 0, successCount: 0, skipped: 0, lastTaskId: '', lastTime: '', anySubmitted: false, runCount: 0, autoSubmit: false };
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
    { title: '自动提交', width: 80, render: (_: any, r: any) => (
      <Switch size="small" checked={r.autoSubmit} onChange={async (v) => {
        try { await dcApi.setAutoSubmit(r.sourceId, v); load(); } catch { }
      }} />
    ) },
    { title: '操作', width: 260, render: (_: any, r: any) => (
      <RowActions actions={[
        { key: 'view', label: '查看数据', icon: <EyeOutlined />, onClick: () => { setSelectedTask({ id: '', sourceId: r.sourceId, sourceName: r.sourceName } as any); setViewModal(true); } },
        { key: 'map', label: '映射/修正', icon: <EditOutlined />, tone: 'primary', onClick: async () => { const res = await dcApi.getTasks({ page: 0, size: 1, sourceId: r.sourceId }); if (res.items[0]) { setSelectedTask(res.items[0]); setMapModal(true); } } },
        !r.anySubmitted && { key: 'submit', label: '提交', icon: <CheckCircleOutlined />, tone: 'success', onClick: async () => { const res = await dcApi.getTasks({ page: 0, size: 99999, sourceId: r.sourceId }); for (const t of res.items) { await dcApi.submitTask(t.id); } load(); message.success('已提交'); } },
        r.anySubmitted && { key: 'unsubmit', label: '取消提交', icon: <RollbackOutlined />, tone: 'warning', confirmTitle: '取消此采集源已提交到汇总的数据？', onClick: async () => { const res = await dcApi.getTasks({ page: 0, size: 99999, sourceId: r.sourceId }); for (const t of res.items.filter(x => x.isSubmitted)) { await dcApi.unsubmitTask(t.id); } load(); message.success('已取消提交'); } },
        { key: 'delete', label: '删除', icon: <DeleteOutlined />, tone: 'danger', confirmTitle: '删除此源所有数据？', onClick: async () => { const res = await dcApi.getTasks({ page: 0, size: 99999, sourceId: r.sourceId }); for (const t of res.items) { await dcApi.deleteTask(t.id); } load(); message.success('已删除'); } },
      ]} />
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
      <Modal title={`映射/修正规则 - ${selectedTask?.sourceName}`} open={mapModal} onCancel={() => setMapModal(false)} width={1040} footer={null}>
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
  const rows = data.map(r => {
    let raw = {};
    let cleaned: any = {};
    try { raw = JSON.parse(r.rawDataJson); } catch { }
    try { cleaned = JSON.parse(r.cleanedDataJson || '{}'); } catch { }
    return { ...r, _p: raw, _excluded: cleaned?._excludedFromSummary === true || cleaned?._excludedFromSummary === 'true' || cleaned?._excludedFromSummary === 1 };
  });
  const firstKeys = rows.length > 0 ? Object.keys(rows[0]._p) : [];
  const extraKeys = rows.flatMap(r => Object.keys(r._p)).filter(k => !firstKeys.includes(k));
  const keys = [...firstKeys, ...extraKeys];
  return <Table columns={[{ title: '状态', dataIndex: 'status', width: 90, fixed: 'left' as const, render: (v: string, r: any) => r._excluded ? <Tag color="orange">排除汇总</Tag> : <Tag>{v}</Tag> },
    ...keys.map(k => ({ title: COLUMN_NAMES[k] || k, width: 140, ellipsis: false as const, render: (_: any, r: any) => { const v = r._p?.[k]; return v != null ? String(v) : ''; } })),
  ]} dataSource={rows} rowKey="id" loading={loading} scroll={{ x: Math.max(900, keys.length * 140) }} size="small"
    pagination={{ defaultPageSize: 20, showTotal: (t: number) => `${t} 条`, showSizeChanger: true }} />;
}

function isExcludedFromSummary(cleanedDataJson?: string | null) {
  if (!cleanedDataJson) return false;
  try {
    const parsed = JSON.parse(cleanedDataJson);
    return parsed?._excludedFromSummary === true || parsed?._excludedFromSummary === 'true' || parsed?._excludedFromSummary === 1;
  } catch {
    return false;
  }
}

function formatSummaryDate(value: unknown) {
  if (value == null || value === '') return '';
  const text = String(value).trim();
  const match = text.match(/^(\d{4})[-/](\d{1,2})[-/](\d{1,2})/);
  if (match) return `${match[1]}-${match[2].padStart(2, '0')}-${match[3].padStart(2, '0')}`;
  const date = new Date(text);
  if (Number.isNaN(date.getTime())) return text;
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function TaskMapView({ sourceName, sourceId, onSaved }: { taskId?: string; sourceName: string; sourceId: string; onSaved: () => void }) {
  const [sourceFields, setSourceFields] = useState<string[]>([]);
  const [previewRows, setPreviewRows] = useState<any[]>([]);
  const [mappings, setMappings] = useState<Record<string, string>>({});
  const [rules, setRules] = useState<UiPostRule[]>([]);
  const [saving, setSaving] = useState(false);
  const [applying, setApplying] = useState(false);
  const canWrite = useAuthStore(s => s.canWrite);
  const summaryFieldOptions = SUMMARY_COLUMNS.map(c => ({ label: c.title, value: c.key }));

  useEffect(() => {
    if (!sourceId) return;
    (async () => {
      try {
        const r = await dcApi.getRecords({ page: 0, size: 5, sourceId });
        const keys = new Set<string>();
        const parsed = r.items.map(x => { try { return JSON.parse(x.rawDataJson); } catch { return {}; } });
        parsed.forEach(p => Object.keys(p).forEach(k => keys.add(k)));
        const firstKeys = parsed.length > 0 ? Object.keys(parsed[0]) : [];
        const extra = Array.from(keys).filter(k => !firstKeys.includes(k));
        setSourceFields([...firstKeys, ...extra]);
        setPreviewRows(parsed);

        const source = await dcApi.getSourceById(sourceId);
        if (source?.transformConfig) {
          const tc = JSON.parse(source.transformConfig || '{}');
          const ex: Record<string, string> = {};
          if (tc.fieldMappings) Object.entries(tc.fieldMappings).forEach(([t, c]: [string, any]) => { ex[t] = c.source || t; });
          if (tc.sourceType) ex.DataType = tc.sourceType;
          setMappings(ex);
          setRules((tc.postProcessRules || []).map((rule: any) => {
            const update = Object.entries(rule.updates || {})[0] as [string, any] | undefined;
            return {
              name: rule.name || '',
              enabled: rule.enabled !== false,
              action: rule.action === 'exclude' ? 'exclude' : 'update',
              excludeReason: rule.excludeReason || '',
              conditions: rule.conditions?.length ? rule.conditions : [{ field: 'DrugName', operator: 'equals', value: '' }],
              targetField: update?.[0] || 'Specification',
              newValue: update?.[1] != null ? String(update[1]) : '',
            };
          }));
        }
      } catch { }
    })();
  }, [sourceId]);

  const buildTransformConfig = async () => {
    const fm: Record<string, any> = {};
    Object.entries(mappings).forEach(([t, s]) => { if (s && t !== 'DataType') fm[t] = { source: s }; });
    const source = await dcApi.getSourceById(sourceId);
    let tc: any = {};
    try { tc = JSON.parse(source?.transformConfig || '{}'); } catch { }
    tc = {
      ...tc,
      fieldMappings: fm,
      sourceType: mappings.DataType || '',
      postProcessRules: rules
        .filter(r => r.action === 'exclude' || (r.targetField && r.newValue !== ''))
        .map(r => ({
          name: r.name || '',
          enabled: r.enabled,
          action: r.action,
          excludeReason: r.excludeReason?.trim() || '',
          conditions: (r.conditions || [])
            .filter(c => c.field && c.operator)
            .map(c => ({ ...c, value: c.value?.trim() || '' })),
          updates: r.action === 'update' ? { [r.targetField]: r.newValue.trim() } : {},
        })),
    };
    return { source, transformConfig: JSON.stringify(tc) };
  };

  const save = async (closeAfterSave = true) => {
    setSaving(true);
    try {
      const { source, transformConfig } = await buildTransformConfig();
      await dcApi.updateSource(sourceId, { ...source, transformConfig });
      message.success('保存成功');
      if (closeAfterSave) onSaved();
      return true;
    } catch (e: any) {
      message.error(e?.message || '保存失败');
      return false;
    } finally {
      setSaving(false);
    }
  };

  const applyRules = async () => {
    setApplying(true);
    try {
      const saved = await save(false);
      if (!saved) return;
      const result = await dcApi.reapplyTransform(sourceId);
      message.success(`已处理 ${result.updatedRecords} 条，错误 ${result.errorRecords} 条`);
      onSaved();
    } catch (e: any) {
      message.error(e?.message || '应用失败');
    } finally {
      setApplying(false);
    }
  };

  const updateRule = (index: number, patch: Partial<UiPostRule>) =>
    setRules(prev => prev.map((rule, i) => i === index ? { ...rule, ...patch } : rule));

  const updateCondition = (ruleIndex: number, conditionIndex: number, patch: Partial<PostRuleCondition>) =>
    setRules(prev => prev.map((rule, i) => {
      if (i !== ruleIndex) return rule;
      return { ...rule, conditions: rule.conditions.map((c, j) => j === conditionIndex ? { ...c, ...patch } : c) };
    }));

  return <div>
    <div style={{ marginBottom: 12, display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
      <span>采集源：</span><Tag color="blue">{sourceName}</Tag>
      {canWrite && <Button type="primary" size="small" loading={saving} onClick={() => save(true)}>保存规则</Button>}
      {canWrite && <Button size="small" loading={applying} onClick={applyRules}>应用到已采集数据</Button>}
    </div>
    <Table size="small" dataSource={[
      { key: '_source', title: '采集源', source: sourceName || '-', readonly: true },
      ...SUMMARY_COLUMNS.map(c => ({ key: c.key, title: c.title, source: mappings[c.key] || '', isSelect: (c as any).isSelect, options: (c as any).options, readonly: false })),
    ]} rowKey="key" pagination={false}
      columns={[{ title: '汇总列', dataIndex: 'title', width: 80 }, { title: '映射值', dataIndex: 'source', width: 250,
        render: (v: string, r: any) => r.readonly
          ? <Tag color="blue">{v}</Tag>
          : r.isSelect
          ? <Select value={v || undefined} placeholder="选择" style={{ width: 220 }} onChange={(val) => setMappings(p => ({ ...p, [r.key]: val || '' }))} options={(r.options || []).map((o: string) => ({ label: o, value: o }))} />
          : <Select value={v || undefined} allowClear placeholder="选择源字段" style={{ width: 220 }} onChange={(val) => setMappings(p => ({ ...p, [r.key]: val || '' }))} options={sourceFields.map(f => ({ label: `${COLUMN_NAMES[f] || f}`, value: f }))} />,
      }]} />

    <div style={{ marginTop: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
        <strong>数据修正规则</strong>
        {canWrite && <Button size="small" onClick={() => setRules(prev => [...prev, createRule()])}>新增空白规则</Button>}
      </div>
      {rules.length === 0 && <div style={{ color: '#8c8c8c', padding: '8px 0' }}>暂无修正规则</div>}
      {rules.map((rule, ruleIndex) => (
        <div key={ruleIndex} style={{ border: '1px solid #f0f0f0', borderRadius: 6, padding: 12, marginBottom: 10 }}>
          <Space wrap align="start">
            <Switch size="small" checked={rule.enabled} onChange={(enabled) => updateRule(ruleIndex, { enabled })} />
            <Input placeholder="规则名称" value={rule.name} style={{ width: 160 }} onChange={e => updateRule(ruleIndex, { name: e.target.value })} />
            <span>动作</span>
            <Select value={rule.action} style={{ width: 130 }} options={[
              { label: '修改字段', value: 'update' },
              { label: '排除汇总', value: 'exclude' },
            ]} onChange={action => updateRule(ruleIndex, { action })} />
            {rule.action === 'update' ? <>
              <span>设置</span>
              <Select value={rule.targetField} style={{ width: 140 }} options={summaryFieldOptions} onChange={targetField => updateRule(ruleIndex, { targetField })} />
              <Input placeholder="新值" value={rule.newValue} style={{ width: 180 }} onChange={e => updateRule(ruleIndex, { newValue: e.target.value })} />
            </> : <Input placeholder="排除原因（可选）" value={rule.excludeReason} style={{ width: 220 }} onChange={e => updateRule(ruleIndex, { excludeReason: e.target.value })} />}
            <Button size="small" danger onClick={() => setRules(prev => prev.filter((_, i) => i !== ruleIndex))}>删除</Button>
          </Space>
          <div style={{ marginTop: 8 }}>
            {rule.conditions.map((condition, conditionIndex) => (
              <Space key={conditionIndex} wrap style={{ marginBottom: 6 }}>
                <span>{conditionIndex === 0 ? '当' : '且'}</span>
                <Select value={condition.field} style={{ width: 140 }} options={summaryFieldOptions} onChange={field => updateCondition(ruleIndex, conditionIndex, { field })} />
                <Select value={condition.operator} style={{ width: 100 }} options={CONDITION_OPERATORS} onChange={operator => updateCondition(ruleIndex, conditionIndex, { operator })} />
                {!['empty', 'notEmpty'].includes(condition.operator) && <Input value={condition.value} placeholder="匹配值" style={{ width: 200 }} onChange={e => updateCondition(ruleIndex, conditionIndex, { value: e.target.value })} />}
                <Button size="small" onClick={() => updateRule(ruleIndex, { conditions: rule.conditions.filter((_, i) => i !== conditionIndex) })}>移除</Button>
              </Space>
            ))}
            <Button size="small" onClick={() => updateRule(ruleIndex, { conditions: [...rule.conditions, { field: 'Specification', operator: 'equals', value: '' }] })}>添加条件</Button>
          </div>
        </div>
      ))}
    </div>

    {previewRows.length > 0 && <>
      <h4 style={{ marginTop: 12 }}>数据预览（前 {previewRows.length} 行）</h4>
      <Table size="small" dataSource={previewRows.map((r, i) => ({ ...r, _idx: i }))} rowKey="_idx" scroll={{ x: sourceFields.length * 130 }}
        pagination={false}
        columns={sourceFields.map(f => ({ title: COLUMN_NAMES[f] || f, dataIndex: f, width: 130, ellipsis: true as const, render: (v: any) => v != null ? String(v) : '' }))} />
    </>}
  </div>;
}

function SummaryTab() {
  const [data, setData] = useState<CollectionSummaryRecordDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [sources, setSources] = useState<DropdownItem[]>([]);
  const [sourceFilter, setSourceFilter] = useState<string | undefined>();
  const [dataTypeFilter, setDataTypeFilter] = useState<string | undefined>();
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [dateRange, setDateRange] = useState<any>(null);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(20);
  const [total, setTotal] = useState(0);

  const loadSources = useCallback(async () => {
    try {
      const freshSources = await dcApi.getSummarySourceDropdown();
      setSources(freshSources);
      if (sourceFilter && !freshSources.some(s => s.id === sourceFilter)) {
        setSourceFilter(undefined);
        setPage(0);
      }
    } catch { }
  }, [sourceFilter]);

  useEffect(() => {
    loadSources();
  }, [loadSources]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await dcApi.getSummary({
        page,
        size: pageSize,
        sourceId: sourceFilter,
        search: search || undefined,
        dataType: dataTypeFilter,
        dateFrom: dateRange?.[0]?.format('YYYY-MM-DD'),
        dateTo: dateRange?.[1]?.format('YYYY-MM-DD'),
      });
      setData(result.items);
      setTotal(result.totalCount);
    } catch (e: any) {
      message.error(e?.message || '加载采集汇总失败');
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, sourceFilter, search, dataTypeFilter, dateRange]);
  useEffect(() => { load(); }, [load]);

  const resetToFirstPage = () => setPage(0);

  const dataTypeOptions = Array.from(new Set([
    ...data.map(x => x.dataType).filter(Boolean),
    '采购流向',
    '销售流向',
  ])).map(value => ({ label: value, value }));

  const summaryFieldMap: Record<string, keyof CollectionSummaryRecordDto> = {
    DataType: 'dataType',
    DrugName: 'drugName',
    Specification: 'specification',
    BatchNo: 'batchNo',
    CompanyName: 'companyName',
    ToCustomerName: 'toCustomerName',
    TaxedUnitPrice: 'taxedUnitPrice',
    Quantity: 'quantity',
    DistributionDate: 'distributionDate',
    Province: 'province',
    ExpiryDate: 'expiryDate',
  };

  const renderSyncTarget = (dataType?: string) => {
    if ((dataType || '').includes('采购')) return <Tag color="cyan">采购管理</Tag>;
    if ((dataType || '').includes('销售')) return <Tag color="purple">销售管理</Tag>;
    return <Tag>待识别</Tag>;
  };

  const columns: ColumnsType<any> = [
    { title: '采集源', dataIndex: 'sourceName', width: 120, fixed: 'left' as const, render: (v: string) => <Tag color="blue">{v}</Tag> },
    { title: '同步状态', dataIndex: 'syncStatus', width: 90, fixed: 'left' as const, render: (_: string, r: CollectionSummaryRecordDto) => (
      r.targetEntityId ? <Tag color="green">已同步</Tag> : <Tag color="orange">待同步</Tag>
    ) },
    { title: '同步去向', dataIndex: 'dataType', width: 90, fixed: 'left' as const, render: renderSyncTarget },
    ...SUMMARY_COLUMNS.map(c => ({ title: c.title, width: c.width, ellipsis: true as const,
      render: (_: any, r: CollectionSummaryRecordDto) => {
        const v = r[summaryFieldMap[c.key]];
        if (v == null || v === '') return '';
        return (c.key === 'DistributionDate' || c.key === 'ExpiryDate') ? formatSummaryDate(v) : String(v);
      },
    })),
  ];

  return <div>
    <div style={{ marginBottom: 16, display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
      <Input.Search value={searchInput} allowClear placeholder="搜索药品/客户/批号/地区" style={{ width: 260 }}
        onChange={e => setSearchInput(e.target.value)}
        onSearch={v => { setSearch(v.trim()); resetToFirstPage(); }} />
      <Select value={sourceFilter} allowClear placeholder="按采集源筛选" style={{ width: 200 }}
        onChange={(v) => { setSourceFilter(v); resetToFirstPage(); }}
        options={sources.map(s => ({ label: s.name, value: s.id }))} />
      <Select value={dataTypeFilter} allowClear placeholder="按数据类型筛选" style={{ width: 160 }}
        onChange={(v) => { setDataTypeFilter(v); resetToFirstPage(); }}
        options={dataTypeOptions} />
      <DatePicker.RangePicker value={dateRange} onChange={(v) => { setDateRange(v); resetToFirstPage(); }} />
      <Button icon={<ReloadOutlined />} onClick={load}>刷新</Button>
      <Button icon={<ThunderboltOutlined />} loading={syncing} onClick={async () => {
        setSyncing(true);
        try {
          const result = await dcApi.syncSummary(sourceFilter);
          message.success(`重新同步完成：业务流向 ${result.createdDistributionRecords} 条，跳过 ${result.skippedRecords} 条`);
          await loadSources();
          load();
        } catch (e: any) {
          message.error(e?.message || '重新同步失败');
        } finally {
          setSyncing(false);
        }
      }}>重新同步</Button>
      <Tag color="processing">业务数据实时同步</Tag>
      <Popconfirm title="清空后会从汇总移除，并撤销由采集汇总同步出的业务流向记录" onConfirm={async () => { await dcApi.clearSummary(); resetToFirstPage(); await loadSources(); load(); message.success('已清空'); }}>
        <Button danger>清空</Button>
      </Popconfirm>
      <Tag>{total} 条</Tag></div>
    <Table columns={columns} dataSource={data} rowKey="id" loading={loading} scroll={{ x: 1800 }} size="small"
      pagination={{
        current: page + 1,
        pageSize,
        total,
        showTotal: (t: number) => `共 ${t} 条`,
        showSizeChanger: true,
        onChange: (nextPage, nextSize) => {
          setPage(nextPage - 1);
          setPageSize(nextSize);
        },
      }} />
  </div>;
}
