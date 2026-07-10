import { useEffect, useMemo, useRef, useState } from 'react';
import { Card, Col, Empty, Row, Select } from 'antd';
import {
  AlertOutlined,
  EnvironmentOutlined,
  TeamOutlined,
  UserSwitchOutlined,
} from '@ant-design/icons';
import {
  Bar,
  BarChart,
  Cell,
  LabelList,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import apiClient from '../api/client';
import type { ApiResponse } from '../types';

interface CountItem {
  name: string;
  count: number;
}

interface Summary {
  distributorCount: number;
  endCustomerCount: number;
  drugCount: number;
  lowStockCount: number;
  distByLevel: CountItem[];
  endByProv: CountItem[];
  endByType: CountItem[];
}

const COLORS = ['#3b82f6', '#18c18f', '#f97316', '#8b5cf6', '#06b6d4', '#16a34a', '#94a3b8', '#2563eb'];
const MAP_PROVINCE_TOTAL = 34;
const MAP_VIEWBOX_WIDTH = 1080;
const MAP_VIEWBOX_HEIGHT = 860;
const MAP_VIEWBOX_PADDING_X = 18;
const MAP_VIEWBOX_PADDING_Y = 20;
const SMALL_REGION_NAMES = new Set(['北京市', '天津市', '上海市', '香港特别行政区', '澳门特别行政区']);

const PROV_MAP: Record<string, string> = {
  北京: '北京市',
  天津: '天津市',
  上海: '上海市',
  重庆: '重庆市',
  河北: '河北省',
  山西: '山西省',
  辽宁: '辽宁省',
  吉林: '吉林省',
  黑龙江: '黑龙江省',
  江苏: '江苏省',
  浙江: '浙江省',
  安徽: '安徽省',
  福建: '福建省',
  江西: '江西省',
  山东: '山东省',
  河南: '河南省',
  湖北: '湖北省',
  湖南: '湖南省',
  广东: '广东省',
  海南: '海南省',
  四川: '四川省',
  贵州: '贵州省',
  云南: '云南省',
  陕西: '陕西省',
  甘肃: '甘肃省',
  青海: '青海省',
  台湾: '台湾省',
  内蒙古: '内蒙古自治区',
  广西: '广西壮族自治区',
  西藏: '西藏自治区',
  宁夏: '宁夏回族自治区',
  新疆: '新疆维吾尔自治区',
  香港: '香港特别行政区',
  澳门: '澳门特别行政区',
};

const PROVINCE_FULL_NAMES = Object.values(PROV_MAP);

function normalizeProvinceName(value?: string) {
  const name = (value ?? '').replace(/\s/g, '').trim();
  if (!name || name === '未知') return '';

  if (PROV_MAP[name]) return PROV_MAP[name];

  const fullName = PROVINCE_FULL_NAMES.find(province => name.startsWith(province));
  if (fullName) return fullName;

  const alias = Object.entries(PROV_MAP).find(([shortName]) => name.startsWith(shortName));
  return alias?.[1] ?? name;
}

function shortProvinceName(value: string) {
  return value
    .replace('维吾尔自治区', '')
    .replace('壮族自治区', '')
    .replace('回族自治区', '')
    .replace('特别行政区', '')
    .replace('自治区', '')
    .replace(/[省市]$/, '');
}

function toCountMap(data: CountItem[] | null) {
  const map = new Map<string, number>();
  (data ?? []).forEach(item => {
    const province = normalizeProvinceName(item.name);
    if (!province) return;
    map.set(province, (map.get(province) ?? 0) + item.count);
  });
  return map;
}

interface ProjectedMapFeature {
  name: string;
  path: string;
  centerX: number;
  centerY: number;
}

interface MapTooltipState {
  name: string;
  value: number;
  share: number;
  x: number;
  y: number;
}

interface ProjectionConfig {
  minX: number;
  maxY: number;
  scale: number;
  offsetX: number;
  offsetY: number;
}

function projectRawPoint(lng: number, lat: number) {
  return [lng, lat] as const;
}

function walkCoordinates(coordinates: any, visit: (lng: number, lat: number) => void) {
  if (!Array.isArray(coordinates)) return;
  if (typeof coordinates[0] === 'number' && typeof coordinates[1] === 'number') {
    visit(coordinates[0], coordinates[1]);
    return;
  }
  coordinates.forEach(item => walkCoordinates(item, visit));
}

function createProjection(features: any[]): ProjectionConfig {
  let minX = Infinity;
  let maxX = -Infinity;
  let minY = Infinity;
  let maxY = -Infinity;

  features.forEach(feature => {
    walkCoordinates(feature.geometry?.coordinates, (lng, lat) => {
      const [x, y] = projectRawPoint(lng, lat);
      minX = Math.min(minX, x);
      maxX = Math.max(maxX, x);
      minY = Math.min(minY, y);
      maxY = Math.max(maxY, y);
    });
  });

  const width = Math.max(maxX - minX, 1);
  const height = Math.max(maxY - minY, 1);
  const usableWidth = MAP_VIEWBOX_WIDTH - MAP_VIEWBOX_PADDING_X * 2;
  const usableHeight = MAP_VIEWBOX_HEIGHT - MAP_VIEWBOX_PADDING_Y * 2;
  const scale = Math.min(usableWidth / width, usableHeight / height);

  return {
    minX,
    maxY,
    scale,
    offsetX: MAP_VIEWBOX_PADDING_X + (usableWidth - width * scale) / 2,
    offsetY: MAP_VIEWBOX_PADDING_Y + (usableHeight - height * scale) / 2,
  };
}

function projectPoint(lng: number, lat: number, projection: ProjectionConfig) {
  const [x, y] = projectRawPoint(lng, lat);
  return {
    x: (x - projection.minX) * projection.scale + projection.offsetX,
    y: (projection.maxY - y) * projection.scale + projection.offsetY,
  };
}

function featurePath(feature: any, projection: ProjectionConfig) {
  const coordinates = feature.geometry?.coordinates;
  if (!coordinates) return '';

  const polygons = feature.geometry.type === 'Polygon' ? [coordinates] : coordinates;
  return polygons
    .map((polygon: any[]) => polygon
      .map((ring: number[][]) => ring
        .map(([lng, lat], index) => {
          const point = projectPoint(lng, lat, projection);
          const command = index === 0 ? 'M' : 'L';
          return `${command}${point.x.toFixed(1)} ${point.y.toFixed(1)}`;
        })
        .join(' ') + ' Z')
      .join(' '))
    .join(' ');
}

function buildMapFeatures(geo: any): ProjectedMapFeature[] {
  const namedFeatures = (geo?.features ?? [])
    .filter((feature: any) => feature.properties?.name && feature.geometry?.coordinates);
  if (namedFeatures.length === 0) return [];

  const projection = createProjection(namedFeatures);

  return namedFeatures.map((feature: any) => {
    const centerSource = feature.properties?.centroid ?? feature.properties?.center;
    const center = Array.isArray(centerSource)
      ? projectPoint(centerSource[0], centerSource[1], projection)
      : { x: MAP_VIEWBOX_WIDTH / 2, y: MAP_VIEWBOX_HEIGHT / 2 };

    return {
      name: feature.properties.name as string,
      path: featurePath(feature, projection),
      centerX: center.x,
      centerY: center.y,
    };
  }).filter(feature => feature.path);
}

function mixHexColor(start: string, end: string, amount: number) {
  const t = Math.max(0, Math.min(1, amount));
  const from = start.match(/\w\w/g)?.map(value => parseInt(value, 16)) ?? [0, 0, 0];
  const to = end.match(/\w\w/g)?.map(value => parseInt(value, 16)) ?? [0, 0, 0];
  const mixed = from.map((value, index) => Math.round(value + (to[index] - value) * t));
  return `#${mixed.map(value => value.toString(16).padStart(2, '0')).join('')}`;
}

function provinceFillColor(value: number, max: number) {
  if (value <= 0) return '#edf2f7';
  const strength = Math.sqrt(value / Math.max(max, 1));
  if (strength < 0.58) return mixHexColor('#dbeafe', '#67e8f9', strength / 0.58);
  return mixHexColor('#67e8f9', '#0f766e', (strength - 0.58) / 0.42);
}

function sumCounts(data?: CountItem[] | null) {
  return (data ?? []).reduce((total, item) => total + item.count, 0);
}

function sortCountItems(data?: CountItem[] | null) {
  return [...(data ?? [])].sort((a, b) => b.count - a.count);
}

function formatNumber(value: number, maximumFractionDigits = 1) {
  return new Intl.NumberFormat('zh-CN', {
    maximumFractionDigits,
  }).format(Number.isFinite(value) ? value : 0);
}

function formatPercent(value: number, maximumFractionDigits = 0) {
  return formatNumber(value, maximumFractionDigits);
}

function topNShare(data: CountItem[], count: number) {
  const total = sumCounts(data);
  if (total <= 0) return 0;
  const topTotal = data.slice(0, count).reduce((sum, item) => sum + item.count, 0);
  return (topTotal / total) * 100;
}

type BadgeTone = 'success' | 'warning' | 'danger' | 'neutral';

function getCoverageStatus(rate: number): { label: string; tone: BadgeTone } {
  if (rate >= 70) return { label: '覆盖充分', tone: 'success' };
  if (rate >= 40) return { label: '持续拓展', tone: 'warning' };
  return { label: '覆盖偏低', tone: 'danger' };
}

function getConcentrationStatus(rate: number): { label: string; tone: BadgeTone } {
  if (rate >= 65) return { label: '集中度高', tone: 'danger' };
  if (rate >= 45) return { label: '集中可控', tone: 'warning' };
  return { label: '分布均衡', tone: 'success' };
}

function getRiskStatus(lowStockCount: number): { label: string; tone: BadgeTone } {
  if (lowStockCount <= 0) return { label: '库存健康', tone: 'success' };
  if (lowStockCount <= 5) return { label: '少量关注', tone: 'warning' };
  return { label: '优先处理', tone: 'danger' };
}

function getDataQualityStatus(rate: number): { label: string; tone: BadgeTone } {
  if (rate <= 0) return { label: '层级完整', tone: 'success' };
  if (rate <= 8) return { label: '少量待补', tone: 'warning' };
  return { label: '需治理', tone: 'danger' };
}

function StatCard({
  title,
  value,
  suffix,
  icon,
  tone,
  loading,
  description,
  badge,
  badgeTone = 'neutral',
  metaLabel,
  metaValue,
}: {
  title: string;
  value: number | string;
  suffix: string;
  icon: React.ReactNode;
  tone: 'blue' | 'teal' | 'amber' | 'red';
  loading: boolean;
  description: string;
  badge: string;
  badgeTone?: BadgeTone;
  metaLabel: string;
  metaValue: string;
}) {
  return (
    <Card className={`dashboard-stat-card dashboard-stat-card-${tone}`} loading={loading}>
      <div className="dashboard-stat">
        <span className="dashboard-stat-icon">{icon}</span>
        <span className="dashboard-stat-copy">
          <span className="dashboard-stat-title-row">
            <span className="dashboard-stat-title">{title}</span>
            <span className={`dashboard-stat-badge dashboard-stat-badge-${badgeTone}`}>{badge}</span>
          </span>
          <span className="dashboard-stat-value">
            {value}
            <span>{suffix}</span>
          </span>
          <span className="dashboard-stat-description">{description}</span>
          <span className="dashboard-stat-footer">
            <small>{metaLabel}</small>
            <strong>{metaValue}</strong>
          </span>
        </span>
      </div>
    </Card>
  );
}

function ChinaMapCard({ data, loading }: { data: CountItem[] | null; loading: boolean }) {
  const mapShellRef = useRef<HTMLDivElement>(null);
  const [mapFeatures, setMapFeatures] = useState<ProjectedMapFeature[]>([]);
  const [hoveredProvince, setHoveredProvince] = useState<MapTooltipState | null>(null);
  const provinceMap = useMemo(() => toCountMap(data), [data]);
  const rankedProvinces = useMemo(
    () => Array.from(provinceMap.entries())
      .map(([name, count]) => ({ name, count }))
      .sort((a, b) => b.count - a.count),
    [provinceMap],
  );
  const displayProvinces = rankedProvinces.slice(0, 10);
  const mapHotspots = rankedProvinces.slice(0, 3);
  const totalCustomers = useMemo(
    () => Array.from(provinceMap.values()).reduce((total, count) => total + count, 0),
    [provinceMap],
  );
  const topProvince = rankedProvinces[0];
  const topCount = topProvince?.count ?? 1;
  const topShare = topProvince && totalCustomers > 0 ? Math.round((topProvince.count / totalCustomers) * 100) : 0;
  const top3Share = Math.round(topNShare(rankedProvinces, 3));
  const coverageRate = Math.round((provinceMap.size / MAP_PROVINCE_TOTAL) * 100);
  const hasMapData = totalCustomers > 0;
  const hoveredFeature = hoveredProvince
    ? mapFeatures.find(feature => feature.name === hoveredProvince.name)
    : null;

  useEffect(() => {
    let disposed = false;

    fetch('/china-geo.json')
      .then(r => r.json())
      .then(geo => {
        if (!disposed) setMapFeatures(buildMapFeatures(geo));
      })
      .catch(() => {
        if (!disposed) setMapFeatures([]);
      });

    return () => {
      disposed = true;
    };
  }, []);

  const updateMapTooltip = (event: React.MouseEvent<SVGElement>, feature: ProjectedMapFeature) => {
    const shell = mapShellRef.current;
    if (!shell) return;

    const rect = shell.getBoundingClientRect();
    const scaleX = rect.width / Math.max(shell.offsetWidth, 1);
    const scaleY = rect.height / Math.max(shell.offsetHeight, 1);
    const value = provinceMap.get(feature.name) ?? 0;

    setHoveredProvince({
      name: feature.name,
      value,
      share: totalCustomers > 0 ? (value / totalCustomers) * 100 : 0,
      x: (event.clientX - rect.left) / Math.max(scaleX, 0.01),
      y: (event.clientY - rect.top) / Math.max(scaleY, 0.01),
    });
  };

  return (
    <Card
      className="dashboard-card dashboard-map-card"
      loading={loading}
      title={(
        <div className="dashboard-map-title">
          <span className="dashboard-title-copy">
            <span className="dashboard-card-title"><EnvironmentOutlined />终端机构地域覆盖</span>
            <small>按省份沉淀终端机构覆盖深度与区域集中度</small>
          </span>
          <span className="dashboard-map-summary">
            <span>覆盖 {coverageRate}%</span>
            <span>Top3 {top3Share}%</span>
          </span>
        </div>
      )}
    >
      <div className="dashboard-map-layout">
        <div
          ref={mapShellRef}
          className="dashboard-map-shell"
          onMouseLeave={() => setHoveredProvince(null)}
        >
          <div className="dashboard-map-stage">
            <svg
              className="dashboard-map-svg"
              viewBox={`0 0 ${MAP_VIEWBOX_WIDTH} ${MAP_VIEWBOX_HEIGHT}`}
              role="img"
              aria-label="终端机构省份构成地图"
            >
              <g>
                {mapFeatures.map(feature => {
                  const value = provinceMap.get(feature.name) ?? 0;
                  const isActive = value > 0;
                  const isHovered = hoveredProvince?.name === feature.name;

                  return (
                    <path
                      key={feature.name}
                      className={`dashboard-map-province${isActive ? ' is-active' : ''}${isHovered ? ' is-hovered' : ''}`}
                      d={feature.path}
                      fill={provinceFillColor(value, topCount)}
                      fillRule="evenodd"
                      onMouseEnter={event => updateMapTooltip(event, feature)}
                      onMouseMove={event => updateMapTooltip(event, feature)}
                    />
                  );
                })}
              </g>
              {hoveredFeature ? (
                <path
                  className="dashboard-map-province-highlight"
                  d={hoveredFeature.path}
                  fill={provinceFillColor(provinceMap.get(hoveredFeature.name) ?? 0, topCount)}
                  fillRule="evenodd"
                />
              ) : null}
              <g className="dashboard-map-small-regions">
                {mapFeatures
                  .filter(feature => SMALL_REGION_NAMES.has(feature.name) && (provinceMap.get(feature.name) ?? 0) > 0)
                  .map(feature => {
                    const value = provinceMap.get(feature.name) ?? 0;
                    const isHovered = hoveredProvince?.name === feature.name;

                    return (
                      <circle
                        key={feature.name}
                        className={`dashboard-map-small-region${isHovered ? ' is-hovered' : ''}`}
                        cx={feature.centerX}
                        cy={feature.centerY}
                        r={isHovered ? 7 : 5.5}
                        fill={provinceFillColor(value, topCount)}
                        onMouseEnter={event => updateMapTooltip(event, feature)}
                        onMouseMove={event => updateMapTooltip(event, feature)}
                      />
                    );
                  })}
              </g>
            </svg>
          </div>
          {hasMapData ? (
            <div className="dashboard-map-hotspots">
              <span>重点省份</span>
              {mapHotspots.map(item => (
                <strong key={item.name}>
                  <i style={{ background: provinceFillColor(item.count, topCount) }} />
                  {shortProvinceName(item.name)} {item.count}家
                </strong>
              ))}
            </div>
          ) : null}
          {hoveredProvince ? (
            <div
              className="dashboard-map-tooltip"
              style={{
                left: hoveredProvince.x,
                top: hoveredProvince.y,
              }}
            >
              <strong>{hoveredProvince.name}</strong>
              <span>终端机构：{hoveredProvince.value} 家</span>
              <span>占比：{hoveredProvince.share.toFixed(1)}%</span>
            </div>
          ) : null}
          {!hasMapData ? (
            <div className="dashboard-map-empty">
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="暂无省份数据" />
            </div>
          ) : (
            <div className="dashboard-map-legend">
              <span>低</span>
              <i />
              <span>高</span>
            </div>
          )}
        </div>
        <div className="dashboard-province-rank">
          <div className="dashboard-rank-head">
            <span>
              省份排行
              <small>按终端机构数量</small>
            </span>
            <strong>Top {displayProvinces.length}</strong>
          </div>
          <div className="dashboard-map-metrics">
            <span>
              <small>覆盖率</small>
              <strong>{coverageRate}%</strong>
            </span>
            <span>
              <small>Top3 集中度</small>
              <strong>{top3Share}%</strong>
            </span>
          </div>
          <div className="dashboard-rank-note">
            领先省份：{topProvince ? `${shortProvinceName(topProvince.name)} ${topShare}%` : '暂无数据'}
          </div>
          {displayProvinces.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="暂无数据" />
          ) : (
            <div className="dashboard-rank-list">
              {displayProvinces.map((item, index) => (
                <div
                  className={`dashboard-rank-row${hoveredProvince?.name === item.name ? ' is-hovered' : ''}`}
                  key={item.name}
                >
                  <span className="dashboard-rank-index">{index + 1}</span>
                  <span className="dashboard-rank-name">{shortProvinceName(item.name)}</span>
                  <span className="dashboard-rank-bar">
                    <i style={{ width: `${Math.max(8, (item.count / topCount) * 100)}%` }} />
                  </span>
                  <span className="dashboard-rank-percent">
                    {totalCustomers > 0 ? `${Math.round((item.count / totalCustomers) * 100)}%` : '0%'}
                  </span>
                  <strong>{item.count}</strong>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </Card>
  );
}

export default function DashboardPage() {
  const [summary, setSummary] = useState<Summary | null>(null);
  const [loading, setLoading] = useState(true);
  const [drugs, setDrugs] = useState<string[]>([]);
  const [filterDrug, setFilterDrug] = useState<string | undefined>();
  const [distByLevel, setDistByLevel] = useState<CountItem[]>([]);

  useEffect(() => {
    setLoading(true);
    apiClient.get<ApiResponse<Summary>>('/dashboard/summary')
      .then(res => {
        setSummary(res.data!);
        setDistByLevel(res.data?.distByLevel ?? []);
      })
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    apiClient.get<ApiResponse<Summary>>('/dashboard/summary', { params: { drugName: filterDrug || undefined } })
      .then(res => setDistByLevel(res.data?.distByLevel ?? []))
      .catch(() => {});
  }, [filterDrug]);

  useEffect(() => {
    import('../api/drugs').then(m => m.getDrugDropdown().then(d => {
      setDrugs([...new Set(d.map(x => x.name.split(' [')[0]).filter(Boolean))].sort());
    }));
  }, []);

  const dealerLevelData = useMemo(
    () => (filterDrug || distByLevel.length > 0 ? distByLevel : summary?.distByLevel ?? []),
    [distByLevel, filterDrug, summary],
  );
  const dealerTotal = dealerLevelData.reduce((total, item) => total + item.count, 0);
  const endByTypeData = useMemo(
    () => sortCountItems(summary?.endByType),
    [summary],
  );
  const provinceAnalysisData = useMemo(() => {
    const provinceMap = toCountMap(summary?.endByProv ?? null);
    return Array.from(provinceMap.entries())
      .map(([name, count]) => ({ name, count }))
      .sort((a, b) => b.count - a.count);
  }, [summary]);
  const distributorCount = summary?.distributorCount ?? 0;
  const endCustomerCount = summary?.endCustomerCount ?? 0;
  const drugCount = summary?.drugCount ?? 0;
  const lowStockCount = summary?.lowStockCount ?? 0;
  const provinceTotal = sumCounts(provinceAnalysisData);
  const coverageRate = (provinceAnalysisData.length / MAP_PROVINCE_TOTAL) * 100;
  const top3ProvinceShare = topNShare(provinceAnalysisData, 3);
  const topProvinceNames = provinceAnalysisData.slice(0, 3).map(item => shortProvinceName(item.name)).join('、') || '暂无';
  const penetration = distributorCount > 0 ? endCustomerCount / distributorCount : 0;
  const endTypeTotal = sumCounts(endByTypeData);
  const topEndType = endByTypeData[0];
  const topEndTypeShare = topEndType && endTypeTotal > 0 ? (topEndType.count / endTypeTotal) * 100 : 0;
  const distributorLevelData = summary?.distByLevel ?? [];
  const distributorLevelTotal = sumCounts(distributorLevelData);
  const unclassifiedCount = distributorLevelData.find(item => item.name.includes('未分'))?.count ?? 0;
  const unclassifiedRate = distributorLevelTotal > 0 ? (unclassifiedCount / distributorLevelTotal) * 100 : 0;
  const riskDensity = drugCount > 0 ? (lowStockCount / drugCount) * 100 : 0;
  const coverageStatus = getCoverageStatus(coverageRate);
  const concentrationStatus = getConcentrationStatus(top3ProvinceShare);
  const riskStatus = getRiskStatus(lowStockCount);
  const dataQualityStatus = getDataQualityStatus(unclassifiedRate);
  const insights = [
    {
      label: '地域覆盖',
      value: `${provinceAnalysisData.length}/${MAP_PROVINCE_TOTAL} 个省级区域`,
      description: `已沉淀 ${provinceTotal || endCustomerCount} 家终端机构，Top3 省份贡献 ${formatPercent(top3ProvinceShare)}%。`,
      tone: 'blue',
    },
    {
      label: '渠道效率',
      value: `${formatNumber(penetration, 1)} 家/经销商`,
      description: `经销商 ${distributorCount} 家，终端机构 ${endCustomerCount} 家，可用于观察渠道承接强度。`,
      tone: 'teal',
    },
    {
      label: '客户结构',
      value: topEndType ? `${topEndType.name} ${formatPercent(topEndTypeShare)}%` : '暂无结构数据',
      description: topEndType ? `主力终端类型为 ${topEndType.name}，需结合区域覆盖判断资源投放。` : '暂无终端机构类型分布。',
      tone: 'amber',
    },
    {
      label: '数据治理',
      value: unclassifiedCount > 0 ? `${unclassifiedCount} 家未分级` : '层级完整',
      description: unclassifiedCount > 0 ? `未分级经销商占 ${formatPercent(unclassifiedRate, 1)}%，建议优先补齐渠道层级。` : '经销商层级字段完整，可直接用于结构分析。',
      tone: 'purple',
    },
  ];

  return (
    <div className="dashboard-page">
      <Row gutter={[16, 16]} className="dashboard-stat-grid">
        <Col xs={24} sm={12} xl={6}>
          <StatCard
            title="渠道规模"
            value={distributorCount}
            suffix="家"
            icon={<TeamOutlined />}
            tone="blue"
            loading={loading}
            badge={dataQualityStatus.label}
            badgeTone={dataQualityStatus.tone}
            description={`经销商层级样本 ${distributorLevelTotal} 家`}
            metaLabel="未分级占比"
            metaValue={`${formatPercent(unclassifiedRate, 1)}%`}
          />
        </Col>
        <Col xs={24} sm={12} xl={6}>
          <StatCard
            title="终端渗透强度"
            value={formatNumber(penetration, 1)}
            suffix="家/经销商"
            icon={<UserSwitchOutlined />}
            tone="teal"
            loading={loading}
            badge={penetration >= 5 ? '承接较强' : '持续培育'}
            badgeTone={penetration >= 5 ? 'success' : 'warning'}
            description={`终端机构 ${endCustomerCount} 家`}
            metaLabel="经销商"
            metaValue={`${distributorCount} 家`}
          />
        </Col>
        <Col xs={24} sm={12} xl={6}>
          <StatCard
            title="地域覆盖率"
            value={formatPercent(coverageRate)}
            suffix="%"
            icon={<EnvironmentOutlined />}
            tone="amber"
            loading={loading}
            badge={coverageStatus.label}
            badgeTone={coverageStatus.tone}
            description={`覆盖 ${provinceAnalysisData.length}/${MAP_PROVINCE_TOTAL} 个省级区域`}
            metaLabel="Top3 省份"
            metaValue={topProvinceNames}
          />
        </Col>
        <Col xs={24} sm={12} xl={6}>
          <StatCard
            title="库存风险项"
            value={lowStockCount}
            suffix="项"
            icon={<AlertOutlined />}
            tone="red"
            loading={loading}
            badge={riskStatus.label}
            badgeTone={riskStatus.tone}
            description={`药品池 ${drugCount} 种，风险密度 ${formatPercent(riskDensity, 1)}%`}
            metaLabel="区域集中度"
            metaValue={`${concentrationStatus.label} ${formatPercent(top3ProvinceShare)}%`}
          />
        </Col>
      </Row>

      {summary ? (
        <>
          <Card className="dashboard-card dashboard-insight-panel" loading={loading}>
            <div className="dashboard-insight-head">
              <span>经营洞察</span>
              <small>基于客户覆盖、渠道层级、地域分布与库存预警的即时诊断</small>
            </div>
            <div className="dashboard-insight-list">
              {insights.map(item => (
                <div className={`dashboard-insight-item dashboard-insight-item-${item.tone}`} key={item.label}>
                  <span>{item.label}</span>
                  <strong>{item.value}</strong>
                  <p>{item.description}</p>
                </div>
              ))}
            </div>
          </Card>

          <Row gutter={[16, 16]} className="dashboard-chart-grid">
            <Col xs={24} xl={10}>
              <Card
                className="dashboard-card"
                loading={loading}
                title={(
                  <div className="dashboard-card-title-row">
                    <span className="dashboard-title-copy">
                      <span>经销商层级结构</span>
                      <small>观察一级、二级、三级渠道的承接比例</small>
                    </span>
                    <Select
                      size="small"
                      placeholder="药品名称"
                      value={filterDrug}
                      onChange={setFilterDrug}
                      allowClear
                      showSearch
                      className="dashboard-drug-select"
                      filterOption={(value, option) => (option?.label as string || '').includes(value)}
                      options={drugs.map(drug => ({ label: drug, value: drug }))}
                    />
                  </div>
                )}
              >
                {dealerLevelData.length === 0 ? (
                  <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="暂无数据" />
                ) : (
                  <div className="dashboard-chart-body">
                    <ResponsiveContainer width="100%" height="100%">
                      <PieChart>
                        <Pie
                          data={dealerLevelData}
                          dataKey="count"
                          nameKey="name"
                          cx="50%"
                          cy="50%"
                          innerRadius={66}
                          outerRadius={112}
                          paddingAngle={4}
                          labelLine={{ stroke: '#94a3b8' }}
                          label={({ name, count, percent }: any) => (
                            percent >= 0.07 ? `${name}: ${count}家` : ''
                          )}
                        >
                          {dealerLevelData.map((_, index) => (
                            <Cell key={index} fill={COLORS[index % COLORS.length]} stroke="#ffffff" strokeWidth={2} />
                          ))}
                        </Pie>
                        <text x="50%" y="47%" textAnchor="middle" dominantBaseline="middle" className="dashboard-pie-total">
                          {dealerTotal}
                        </text>
                        <text x="50%" y="56%" textAnchor="middle" dominantBaseline="middle" className="dashboard-pie-caption">
                          经销商总数
                        </text>
                        <Tooltip formatter={(value: number) => `${value} 家`} />
                        <Legend wrapperStyle={{ fontSize: 12 }} />
                      </PieChart>
                    </ResponsiveContainer>
                  </div>
                )}
              </Card>
            </Col>

            <Col xs={24} xl={14}>
              <Card
                className="dashboard-card"
                title={(
                  <span className="dashboard-title-copy">
                    <span>终端机构类型结构</span>
                    <small>识别主要服务场景与客户组合</small>
                  </span>
                )}
                loading={loading}
              >
                {endByTypeData.length === 0 ? (
                  <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="暂无数据" />
                ) : (
                  <div className="dashboard-chart-body">
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart data={endByTypeData} layout="vertical" margin={{ left: 12, right: 36, top: 6, bottom: 6 }} barGap={6}>
                        <XAxis type="number" hide />
                        <YAxis dataKey="name" type="category" tick={{ fontSize: 12, fill: '#475569' }} width={96} axisLine={false} tickLine={false} />
                        <Tooltip formatter={(value: number) => `${value} 家`} cursor={{ fill: '#f8fafc' }} />
                        <Bar dataKey="count" radius={[0, 6, 6, 0]} barSize={20} background={{ fill: '#eef2f7', radius: [0, 6, 6, 0] }}>
                          {endByTypeData.map((_, index) => (
                            <Cell key={index} fill={COLORS[index % COLORS.length]} />
                          ))}
                          <LabelList dataKey="count" position="right" className="dashboard-bar-label" />
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                )}
              </Card>
            </Col>
          </Row>

          <ChinaMapCard data={summary.endByProv} loading={loading} />
        </>
      ) : !loading ? (
        <Card className="dashboard-card">
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="暂无仪表盘数据" />
        </Card>
      ) : null}
    </div>
  );
}
