import { useEffect, useState, useCallback } from 'react';
import { Table, Button, Space, Input, Empty, Tag, Popconfirm, message } from 'antd';
import { SearchOutlined, ApartmentOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import apiClient from '../../api/client';
import { getDistribution, deleteDistribution } from '../../api/distribution';
import type { ApiResponse, DistributionRecordDto } from '../../types';

// WPF 布局常量
const COL_W = 420, NODE_W = 240, NODE_H = 150, ROW_GAP = 50, TOP_PAD = 50, LEFT_PAD = 16;
const EDGE_NORMAL = '#64B5F6', EDGE_TRANSFER = '#BCAAA4', ARROW_SIZE = 10;

// WPF 配色
const TYPE_STYLE: Record<string, { bg: string; bar: string; text: string; icon: string }> = {
  '生产厂家': { bg: '#E8F5E9', bar: '#1E8E3E', text: '#1B5E20', icon: '🏭' },
  '供应商':   { bg: '#E8F5E9', bar: '#2E7D32', text: '#2E7D32', icon: '🏭' },
  '生产出厂': { bg: '#E8F5E9', bar: '#1E8E3E', text: '#1B5E20', icon: '🏭' },
  '一级经销': { bg: '#E3F2FD', bar: '#1976D2', text: '#1565C0', icon: '🏢' },
  '二级经销': { bg: '#E0F2F1', bar: '#00897B', text: '#00695C', icon: '🏢' },
  '三级经销': { bg: '#F3E5F5', bar: '#8E24AA', text: '#6A1B9A', icon: '🏢' },
  '经销商':   { bg: '#F5F5F5', bar: '#757575', text: '#616161', icon: '🏢' },
  '终端销售': { bg: '#FFF3E0', bar: '#FF9800', text: '#E65100', icon: '🏥' },
};
const TIER_LABELS = ['生产厂家', '一级经销', '二级经销', '三级经销', '终端销售'];

export default function DistributionPage() {
  const [data, setData] = useState<DistributionRecordDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [filteredCount, setFilteredCount] = useState(0);
  const [searchDrug, setSearchDrug] = useState('');
  const [searchBatch, setSearchBatch] = useState('');
  const [showTrace, setShowTrace] = useState(false);
  const [traceData, setTraceData] = useState<any>(null);

  const load = useCallback(async (drug = searchDrug, batch = searchBatch) => {
    setLoading(true);
    try {
      const res = await getDistribution({ page: 0, size: 99999, drugName: drug || undefined, batchNo: batch || undefined });
      setData(res.items);
    } finally { setLoading(false); }
  }, [searchDrug, searchBatch]);

  useEffect(() => { load(); }, []);

  const handleSearch = () => { setShowTrace(false); load(searchDrug, searchBatch); };
  const handleTrace = async () => {
    if (!searchBatch.trim()) { message.warning('请输入批号'); return; }
    setLoading(true);
    try {
      const res = await apiClient.get<ApiResponse<any>>('/distribution/trace', {
        params: { batchNo: searchBatch.trim(), drugId: '00000000-0000-0000-0000-000000000000' },
      });
      const graph = res.data;
      setTraceData(graph);
      const hasNodes = graph && graph.nodes && Object.keys(graph.nodes).length > 0;
      setShowTrace(!!hasNodes);
      if (!hasNodes) message.info('未找到该批号的流向数据');
    } catch (err: any) { setShowTrace(false); setTraceData(null); message.error('流向追溯失败'); }
    finally { setLoading(false); }
  };
  const handleDelete = async (id: string) => { await deleteDistribution(id); load(); };

  const columns: ColumnsType<DistributionRecordDto> = [
    { title: '药品名称', dataIndex: 'drugName', width: 130 }, { title: '规格', dataIndex: 'specification', width: 90 },
    { title: '批号', dataIndex: 'batchNo', width: 110 }, { title: '来源', dataIndex: 'fromCustomerName', width: 140 },
    { title: '去向', dataIndex: 'toCustomerName', width: 140 }, { title: '数量', dataIndex: 'quantity', width: 70 },
    { title: '日期', dataIndex: 'distributionDate', width: 100, render: (v: string) => v ? v.substring(0, 10) : '-' },
    { title: '操作', width: 80, render: (_, r) => (
        <Popconfirm title="确定删除？" onConfirm={() => handleDelete(r.id)}><Button size="small" danger>删除</Button></Popconfirm>),
    },
  ];

  // ════════════ 节点卡片（匹配 WPF FlowNodeControl） ════════════
  const NodeCard = ({ node }: { node: any }) => {
    const s = TYPE_STYLE[node.type] || { bg: '#FAFAFA', bar: '#BDBDBD', text: '#424242', icon: '📦' };
    const isConflict = node.isChannelConflict;
    return (
      <div style={{
        width: NODE_W, minHeight: NODE_H - 20, background: isConflict ? '#FFF2F0' : s.bg,
        borderLeft: `4px solid ${isConflict ? '#F44336' : s.bar}`,
        borderTop: '1px solid #E0E0E0', borderRight: '1px solid #E0E0E0', borderBottom: '1px solid #E0E0E0',
        borderRadius: '0 6px 6px 0', padding: '8px 10px', fontSize: 12,
        boxShadow: '0 2px 6px rgba(0,0,0,0.08)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', marginBottom: 4 }}>
          <span style={{ fontSize: 14, marginRight: 4 }}>{s.icon}</span>
          <span style={{ fontWeight: 600, color: '#333', fontSize: 12, lineHeight: 1.2 }}>{node.name}</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
          <span style={{ fontSize: 9, fontWeight: 700, color: '#fff', background: s.bar, padding: '1px 6px', borderRadius: 2 }}>{node.type}</span>
          {node.province && <span style={{ fontSize: 9, color: '#999' }}>📍{node.province}</span>}
        </div>
        <div style={{ height: 1, background: '#D8D8D8', margin: '4px 0', opacity: 0.6 }} />
        <div style={{ display: 'flex', gap: 10, fontSize: 11 }}>
          {node.inQuantity > 0 && <span>📥 <b style={{ color: '#1967D2' }}>{node.inQuantity}</b></span>}
          {node.outQuantity > 0 && <span>📤 <b style={{ color: '#EA8600' }}>{node.outQuantity}</b></span>}
          <span>📦 <b style={{ color: '#2E7D32' }}>{node.stockQuantity}</b></span>
        </div>
        {node.date && <div style={{ fontSize: 9, color: '#AAA', marginTop: 2 }}>📅 {node.date.substring(0, 10)}</div>}
        {isConflict && (
          <div style={{ background: '#FFEBEE', borderRadius: 3, padding: '3px 6px', marginTop: 4 }}>
            <span style={{ fontSize: 9, color: '#C62828' }}>⚠ {node.conflictDesc || '串货预警'}</span>
          </div>
        )}
      </div>
    );
  };

  // ════════════ WPF 风格流向图（Canvas + SVG 贝塞尔边） ════════════
  const renderFlowGraph = () => {
    if (!traceData?.tiers) return <Empty description="未找到流向数据" />;
    const tiers = traceData.tiers as Record<string, any[]>;
    const tierKeys = Object.keys(tiers).sort((a, b) => parseInt(a) - parseInt(b));
    const edgesObj = (traceData.edges || {}) as Record<string, any>;
    const nodesMap = (traceData.nodes || {}) as Record<string, any>;

    // 计算节点位置（匹配 WPF 布局算法）
    const pos: Record<string, { x: number; y: number }> = {};
    let maxY = 0, maxTier = 0;
    for (const tk of tierKeys) {
      const t = parseInt(tk); maxTier = Math.max(maxTier, t);
      const x = LEFT_PAD + t * COL_W;
      const nodes = tiers[tk];
      for (let i = 0; i < nodes.length; i++) {
        const y = TOP_PAD + i * (NODE_H + ROW_GAP);
        pos[nodes[i].id] = { x, y };
        maxY = Math.max(maxY, y + NODE_H);
      }
    }

    // 计算边在节点侧面的分布偏移（匹配 WPF Distribute 函数）
    const dist = (idx: number, total: number, h: number) => {
      if (total <= 1) return h / 2;
      const s = h / 3, u = h / 3;
      return s + (u / (total - 1)) * idx;
    };

    // 统计每个节点的出边/入边数量
    const outDeg: Record<string, number> = {}, inDeg: Record<string, number> = {};
    const outIdx: Record<string, number> = {}, inIdx: Record<string, number> = {};
    Object.values(edgesObj).forEach((e: any) => {
      outDeg[e.sourceId] = (outDeg[e.sourceId] || 0) + 1;
      inDeg[e.targetId] = (inDeg[e.targetId] || 0) + 1;
    });

    const canvasW = LEFT_PAD + (maxTier + 1) * COL_W + 80;
    const canvasH = maxY + TOP_PAD + 40;

    // 构建 SVG 边
    const edges = Object.entries(edgesObj).map(([key, e]: [string, any]) => {
      const sp = pos[e.sourceId], tp = pos[e.targetId];
      if (!sp || !tp) return null;
      const isTransfer = e.isTransfer;
      const isReturn = e.isReturn;
      const color = isTransfer ? EDGE_TRANSFER : EDGE_NORMAL;

      // 出/入端口 Y 偏移
      const outT = Math.max(1, outDeg[e.sourceId] || 1);
      const inT = Math.max(1, inDeg[e.targetId] || 1);
      outIdx[e.sourceId] = (outIdx[e.sourceId] || 0);
      inIdx[e.targetId] = (inIdx[e.targetId] || 0);
      const yo = dist(outIdx[e.sourceId]++, outT, NODE_H);
      const yi = dist(inIdx[e.targetId]++, inT, NODE_H);

      // 起始点：源节点右边缘；终点：目标节点左边缘
      const x1 = sp.x + NODE_W, y1 = sp.y + yo;
      const x2 = tp.x, y2 = tp.y + yi;

      // 贝塞尔控制点（匹配 WPF: cx1 = bx1 + dx * 0.55, cx2 = x2 - dx * 0.55）
      const dx = Math.abs(x2 - x1), s = 0.55;
      const cx1 = x1 + dx * s, cy1 = y1;
      const cx2 = x2 - dx * s, cy2 = y2;

      // 箭头（指向目标）
      return (
        <g key={key}>
          {/* 贝塞尔曲线 */}
          <path d={`M ${x1} ${y1} C ${cx1} ${cy1}, ${cx2} ${cy2}, ${x2} ${y2}`}
            fill="none" stroke={color} strokeWidth={isTransfer ? 1 : 1.8}
            strokeDasharray={isTransfer || isReturn ? '6,4' : undefined}
            opacity={isTransfer ? 0.5 : 0.7}
            markerEnd={`url(#arrow${isTransfer ? 'T' : 'N'}${isReturn ? 'R' : ''})`}
          />
          {/* 标签：数量 + 日期 */}
          <text x={(x1 + x2) / 2} y={(y1 + y2) / 2 - 6} textAnchor="middle"
            fontSize="10" fill="#666" fontWeight={600}>
            {isReturn ? '↩' : ''}{Math.abs(e.quantity)}盒
          </text>
        </g>
      );
    }).filter(Boolean);

    return (
      <div style={{ position: 'relative', width: canvasW, height: canvasH }}>
        {/* SVG 边层（z-index: 0） */}
        <svg style={{ position: 'absolute', inset: 0, width: canvasW, height: canvasH, zIndex: 0, overflow: 'visible' }}>
          <defs>
            <marker id="arrowN" viewBox="0 0 10 10" refX={10} refY={5} markerWidth={8} markerHeight={8} orient="auto">
              <path d="M 0 0 L 10 5 L 0 10 z" fill={EDGE_NORMAL} />
            </marker>
            <marker id="arrowT" viewBox="0 0 10 10" refX={10} refY={5} markerWidth={8} markerHeight={8} orient="auto">
              <path d="M 0 0 L 10 5 L 0 10 z" fill={EDGE_TRANSFER} />
            </marker>
            <marker id="arrowNR" viewBox="0 0 10 10" refX={0} refY={5} markerWidth={8} markerHeight={8} orient="auto">
              <path d="M 10 0 L 0 5 L 10 10 z" fill={EDGE_NORMAL} />
            </marker>
          </defs>
          {edges}
        </svg>
        {/* 节点层（z-index: 1） */}
        {Object.entries(pos).map(([id, p]) => (
          <div key={id} style={{ position: 'absolute', left: p.x, top: p.y, zIndex: 1 }}>
            <NodeCard node={nodesMap[id]} />
          </div>
        ))}
        {/* 层标签 */}
        {tierKeys.map(tk => {
          const nodes = tiers[tk]; const t = parseInt(tk);
          const firstNode = nodes.length > 0 ? pos[nodes[0].id] : null;
          return (
            <div key={`label-${tk}`} style={{
              position: 'absolute', left: firstNode ? firstNode.x : LEFT_PAD + t * COL_W,
              top: 8, zIndex: 2, fontWeight: 700, fontSize: 12, color: '#1976D2',
            }}>
              {TIER_LABELS[t] || `第${tk}层`}
            </div>
          );
        })}
      </div>
    );
  };

  return (
    <div>
      <h2 style={{ marginBottom: 16 }}>🔍 流向查询</h2>
      <Space wrap style={{ marginBottom: 16 }}>
        <Input placeholder="批号" value={searchBatch} onChange={e => setSearchBatch(e.target.value)}
          onPressEnter={handleSearch} style={{ width: 160 }} allowClear />
        <Input placeholder="药品名称" value={searchDrug} onChange={e => setSearchDrug(e.target.value)}
          onPressEnter={handleSearch} style={{ width: 140 }} allowClear />
        <Button type="primary" onClick={handleSearch}>查询</Button>
        <Button icon={<ApartmentOutlined />} onClick={handleTrace} disabled={!searchBatch.trim()}>流向追溯</Button>
      </Space>
      {showTrace && traceData ? (
        <>
          {/* 头部横幅 */}
          <div style={{ background: '#1565C0', borderRadius: 12, padding: '16px 20px', marginBottom: 10, color: '#fff', boxShadow: '0 3px 10px rgba(0,0,0,0.15)' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontSize: 18 }}>💊</span><span style={{ fontSize: 16, fontWeight: 700 }}>药品完整流向图</span>
            </div>
            <div style={{ display: 'flex', gap: 16, marginTop: 8 }}>
              <span style={{ fontSize: 14, fontWeight: 600, color: '#90CAF9' }}>{traceData.drugName || ''}</span>
              <span style={{ fontSize: 13, color: '#64B5F6' }}>批号: {traceData.batchNo || searchBatch}</span>
            </div>
            {traceData.supplierName && (
              <div style={{ marginTop: 6, fontSize: 13, color: '#BBDEFB' }}>🏭 来源: {traceData.supplierName}</div>
            )}
            <div style={{ marginTop: 6, fontSize: 11, color: '#90CAF9' }}>涉及 {Object.keys(traceData.nodes || {}).length} 个节点 · {Object.keys(traceData.edges || {}).length} 条流向</div>
          </div>
          {/* 方向指示器 */}
          <div style={{ background: '#F5F5F5', border: '1px solid #E0E0E0', borderRadius: 6, padding: '6px 12px', marginBottom: 10, fontSize: 11, color: '#888', display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ color: '#1976D2', fontSize: 14 }}>▶</span>
            <span>流向: 生产厂家 → 一级经销 → 二级经销 → 三级经销 → 终端销售</span>
            <span style={{ marginLeft: 12 }}>——实线=分销</span>
            <span style={{ borderBottom: '2px dashed #BCAAA4', paddingBottom: 1 }}>- -虚线=调拨</span>
          </div>
          {/* Canvas 流向图 */}
          <div style={{ background: '#FAFBFC', borderRadius: 8, border: '1px solid #E8E8E8', overflow: 'auto', maxHeight: '70vh' }}>
            {renderFlowGraph()}
          </div>
          <Button style={{ marginTop: 12 }} onClick={() => setShowTrace(false)}>返回列表</Button>
        </>
      ) : (
        <Table rowKey="id" columns={columns} dataSource={data} loading={loading}
          onChange={(_p, _f, _s, extra) => setFilteredCount(extra.currentDataSource.length)}
          pagination={{ defaultPageSize: 10, showTotal: () => `共 ${data.length} 条`, showSizeChanger: true }} />
      )}
    </div>
  );
}
