import { useEffect, useState, useRef } from 'react';
import { Row, Col, Card, Statistic, Select } from 'antd';
import { TeamOutlined, MedicineBoxOutlined, WarningOutlined } from '@ant-design/icons';
import { PieChart, Pie, Cell, Tooltip, Legend, BarChart, Bar, XAxis, YAxis, ResponsiveContainer, LabelList } from 'recharts';
import * as echarts from 'echarts';
import apiClient from '../api/client';
import type { ApiResponse } from '../types';

interface Summary {
  distributorCount: number; endCustomerCount: number; drugCount: number; lowStockCount: number;
  distByLevel: { name: string; count: number }[];
  endByProv: { name: string; count: number }[];
  endByType: { name: string; count: number }[];
}

const COLORS = ['#1976D2', '#4CAF50', '#FF9800', '#9C27B0', '#00BCD4', '#F44336', '#795548', '#607D8B', '#E91E63', '#3F51B5'];

const PROV_MAP: Record<string, string> = {
  '北京':'北京市','天津':'天津市','上海':'上海市','重庆':'重庆市',
  '河北':'河北省','山西':'山西省','辽宁':'辽宁省','吉林':'吉林省','黑龙江':'黑龙江省',
  '江苏':'江苏省','浙江':'浙江省','安徽':'安徽省','福建':'福建省','江西':'江西省','山东':'山东省',
  '河南':'河南省','湖北':'湖北省','湖南':'湖南省','广东':'广东省',
  '海南':'海南省','四川':'四川省','贵州':'贵州省','云南':'云南省',
  '陕西':'陕西省','甘肃':'甘肃省','青海':'青海省',
  '内蒙古':'内蒙古自治区','广西':'广西壮族自治区','西藏':'西藏自治区','宁夏':'宁夏回族自治区','新疆':'新疆维吾尔自治区',
};

function ChinaMapCard({ data, loading }: { data: { name: string; count: number }[] | null; loading: boolean }) {
  const ref = useRef<HTMLDivElement>(null);
  const chart = useRef<echarts.ECharts | null>(null);

  useEffect(() => {
    if (!data || loading || !ref.current) return;
    fetch('/china-geo.json')
      .then(r => r.json())
      .then(geo => {
        if (!ref.current) return;
        // 数据映射
        const countMap: Record<string, number> = {};
        data.forEach(d => { if (d.name && d.name !== '未知') { countMap[PROV_MAP[d.name] || d.name] = d.count; } });
        // 所有省份（含值为0的）
        const fullData = geo.features.map((f: any) => ({ name: f.properties.name, value: countMap[f.properties.name] || 0 }));

        if (chart.current) chart.current.dispose();
        echarts.registerMap('china', geo);
        chart.current = echarts.init(ref.current);
        const max = Math.max(...fullData.map((d: any) => d.value), 1);
        chart.current.setOption({
          tooltip: { trigger: 'item', formatter: (p: any) => `${p.name}: ${p.value ?? 0}` },
          visualMap: { min: 0, max, text: ['高', '低'], realtime: false, calculable: true, inRange: { color: ['#BBDEFB', '#1565C0'] } },
          series: [{ type: 'map', map: 'china', roam: true, itemStyle: { borderColor: '#fff', borderWidth: 0.5 }, emphasis: { label: { fontSize: 12, fontWeight: 'bold' }, itemStyle: { areaColor: '#FF9800' } }, label: { show: true, fontSize: 10, color: '#333', textBorderColor: '#fff', textBorderWidth: 2, fontWeight: 'bold', formatter: (p: any) => `${p.name}\n${p.value ?? 0}` }, data: fullData }],
        });
      })
      .catch(() => {});
    return () => { chart.current?.dispose(); chart.current = null; };
  }, [data, loading]);

  return <Card title="终端客户省份构成" loading={loading}><div ref={ref} style={{ width: '100%', height: 700, minHeight: 500 }} /></Card>;
}

export default function DashboardPage() {
  const [summary, setSummary] = useState<Summary | null>(null);
  const [loading, setLoading] = useState(true);
  const [drugs, setDrugs] = useState<string[]>([]);
  const [filterDrug, setFilterDrug] = useState<string | undefined>();
  const [distByLevel, setDistByLevel] = useState<{ name: string; count: number }[]>([]);

  // 全量数据
  useEffect(() => {
    apiClient.get<ApiResponse<Summary>>('/dashboard/summary')
      .then(res => setSummary(res.data!))
      .finally(() => setLoading(false));
  }, []);

  // 经销商级别数据（可选药品筛选）
  useEffect(() => {
    apiClient.get<ApiResponse<Summary>>('/dashboard/summary', { params: { drugName: filterDrug || undefined } })
      .then(res => setDistByLevel(res.data!.distByLevel))
      .catch(() => {});
  }, [filterDrug]);

  useEffect(() => {
    import('../api/drugs').then(m => m.getDrugDropdown().then(d => {
      setDrugs([...new Set(d.map(x => x.name.split(' [')[0]).filter(Boolean))].sort());
    }));
  }, []);

  const s = summary;
  return (
    <div>
      <h2 style={{ marginBottom: 24 }}>📊 首页仪表盘</h2>
      <Row gutter={16} style={{ marginBottom: 24 }}>
        <Col span={6}><Card loading={loading}><Statistic title="经销商数量" value={s?.distributorCount ?? 0} prefix={<TeamOutlined />} suffix="家" valueStyle={{ color: '#1976D2' }} /></Card></Col>
        <Col span={6}><Card loading={loading}><Statistic title="终端客户数量" value={s?.endCustomerCount ?? 0} prefix={<TeamOutlined />} suffix="家" valueStyle={{ color: '#4CAF50' }} /></Card></Col>
        <Col span={6}><Card loading={loading}><Statistic title="药品名称数量" value={s?.drugCount ?? 0} prefix={<MedicineBoxOutlined />} suffix="种" valueStyle={{ color: '#FF9800' }} /></Card></Col>
        <Col span={6}><Card loading={loading}><Statistic title="低库存预警" value={s?.lowStockCount ?? 0} prefix={<WarningOutlined />} suffix="项" valueStyle={{ color: '#F44336' }} /></Card></Col>
      </Row>
      {s && (<>
        <Row gutter={16} style={{ marginBottom: 16 }}>
          <Col span={12}><Card title={<div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}><span>经销商级别构成</span><Select size="small" placeholder="药品名称" value={filterDrug} onChange={setFilterDrug} allowClear showSearch style={{ width: 160 }} filterOption={(v, o) => (o?.label as string || '').includes(v)} options={drugs.map(d => ({ label: d, value: d }))} onClick={e => e.stopPropagation()} /></div>} loading={loading}>
            <ResponsiveContainer width="100%" height={300}><PieChart><Pie data={distByLevel.length > 0 ? distByLevel : s?.distByLevel || []} dataKey="count" nameKey="name" cx="50%" cy="50%" innerRadius={55} outerRadius={105} paddingAngle={5} labelLine={{ stroke: '#999' }} label={({name, count, percent}) => `${name}: ${count}家(${(percent*100).toFixed(0)}%)`} style={{ fontSize: 11 }}>{(distByLevel.length > 0 ? distByLevel : s?.distByLevel || []).map((_, i) => <Cell key={i} fill={COLORS[i]} stroke="#fff" strokeWidth={2} />)}</Pie>
              <text x="50%" y="48%" textAnchor="middle" dominantBaseline="middle" style={{ fontSize: 22, fontWeight: 'bold', fill: '#333' }}>{(distByLevel.length > 0 ? distByLevel : s?.distByLevel || []).reduce((s, i) => s + i.count, 0)}</text>
              <text x="50%" y="56%" textAnchor="middle" dominantBaseline="middle" style={{ fontSize: 12, fill: '#999' }}>经销商总数</text>
              <Tooltip formatter={(v: number) => `${v} 家`} /><Legend wrapperStyle={{ fontSize: 12 }} /></PieChart></ResponsiveContainer>
          </Card></Col>
          <Col span={12}><Card title="终端客户类型构成" loading={loading}>
            <ResponsiveContainer width="100%" height={300}><BarChart data={[...s.endByType].sort((a,b) => b.count - a.count)} layout="vertical" margin={{ left: 10, right: 40 }} barGap={6}><XAxis type="number" hide /><YAxis dataKey="name" type="category" tick={{ fontSize: 12, fill: '#555' }} width={80} axisLine={false} tickLine={false} /><Tooltip formatter={(v: number) => `${v} 家`} cursor={{ fill: '#f5f5f5' }} /><Bar dataKey="count" radius={[0,8,8,0]} barSize={18} background={{ fill: '#f0f0f0', radius: [0,8,8,0] }}>{[...s.endByType].sort((a,b) => b.count - a.count).map((_, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}<LabelList dataKey="count" position="right" style={{ fontSize: 12, fill: '#333', fontWeight: 600 }} /></Bar></BarChart></ResponsiveContainer>
          </Card></Col>
        </Row>
        <Row gutter={16}><Col span={24}><ChinaMapCard data={s.endByProv} loading={loading} /></Col></Row>
      </>)}
    </div>
  );
}
