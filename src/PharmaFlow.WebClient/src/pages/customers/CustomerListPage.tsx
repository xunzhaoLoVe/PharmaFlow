import { useEffect, useState, useCallback } from 'react';
import { Table, Button, Space, Input, Modal, Form, message, Popconfirm, Select, Tabs, Upload } from 'antd';
import { PlusOutlined, SearchOutlined, UploadOutlined, DeleteOutlined } from '@ant-design/icons';
import { useAuthStore } from '../../store/authStore';
import apiClient from '../../api/client';
import type { ColumnsType } from 'antd/es/table';
import * as XLSX from 'xlsx';
import { getCustomers, createCustomer, updateCustomer, deleteCustomer } from '../../api/customers';
import { getDrugDropdown } from '../../api/drugs';
import type { CustomerDto, DropdownItem } from '../../types';

const PROVINCES = ['','北京','天津','河北','山西','内蒙古','辽宁','吉林','黑龙江','上海','江苏','浙江','安徽','福建','江西','山东','河南','湖北','湖南','广东','广西','海南','重庆','四川','贵州','云南','西藏','陕西','甘肃','青海','宁夏','新疆'];
const DISTRIBUTOR_LEVELS = ['','一级经销商','二级经销商','三级经销商'];
const END_TYPES = ['卫生院','医院','其他','卫生服务中心','卫生室','药房','门诊','诊所','卫生服务站','二级经销商'];

export default function CustomerListPage() {
  const canWrite = useAuthStore(s => s.canWrite);
  const [tab, setTab] = useState<string>('distributor');
  const [data, setData] = useState<CustomerDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<CustomerDto | null>(null);
  const [drugOptions, setDrugOptions] = useState<DropdownItem[]>([]);
  const [filteredCount, setFilteredCount] = useState(0);
  const [filteredData, setFilteredData] = useState<CustomerDto[]>([]);
  const [form] = Form.useForm();

  const isDistributor = tab === 'distributor';
  const typeFilter = isDistributor ? '商业' : '非商业';

  const load = useCallback(async (s = search) => {
    setLoading(true);
    try {
      const res = await getCustomers({ page: 0, size: 99999, search: s || undefined, type: typeFilter });
      setData(res.items);
    } finally { setLoading(false); }
  }, [search, typeFilter]);

  useEffect(() => { load(); getDrugDropdown().then(setDrugOptions).catch(() => {}); }, [tab]);
  useEffect(() => { setFilteredCount(data.length); setFilteredData(data); }, [data]);

  const handleSearch = () => { load(search); };
  const handleAdd = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({ customerTypeName: isDistributor ? '商业' : '医院', levelName: '' });
    setModalOpen(true);
  };
  const handleEdit = (record: CustomerDto) => {
    setEditing(record);
    form.setFieldsValue({ code: record.code, name: record.name, province: record.province, drugName: record.drugName, levelName: record.levelName, customerTypeName: record.customerTypeName });
    setModalOpen(true);
  };
  const handleDelete = async (id: string) => { await deleteCustomer(id); message.success('已删除'); load(); };

  // 去重检查
  const isDup = (name: string, province: string, drugName: string, levelName?: string, typeName?: string) => {
    return data.some(d => d.name === name && (d.province || '') === province && (d.drugName || '') === drugName &&
      (isDistributor ? ((d.levelName || '') === (levelName || '')) : ((d.customerTypeName || '') === (typeName || ''))));
  };

  const handleOk = async () => {
    const values = await form.validateFields();
    if (!editing && isDup(values.name, values.province || '', values.drugName || '', values.levelName, values.customerTypeName)) {
      message.warning('已存在相同名称、省份、药品名称、' + (isDistributor ? '级别' : '类型') + '的客户'); return;
    }
    const dto = { ...values, level: values.levelName === '一级经销商' ? 1 : values.levelName === '二级经销商' ? 2 : values.levelName === '三级经销商' ? 3 : null };
    if (editing) { await updateCustomer(editing.id, dto); message.success('更新成功'); }
    else { await createCustomer(dto); message.success('创建成功'); }
    setModalOpen(false); load();
  };

  // Excel 导入
  const handleExcelImport = (file: File) => {
    const reader = new FileReader();
    reader.onload = async (e) => {
      try {
        const wb = XLSX.read(e.target?.result, { type: 'array' });
        const sheet = wb.Sheets[wb.SheetNames[0]];
        const rows = XLSX.utils.sheet_to_json<Record<string, string>>(sheet, { defval: '' });
        if (rows.length === 0) { message.warning('Excel 无数据'); return; }

        const colMap: Record<string, string> = {};
        const headers = Object.keys(rows[0]);
        const findCol = (aliases: string[]) => { for (const h of headers) { const t = h.trim(); if (aliases.includes(t)) return t; } return null; };

        // 建立列映射：别名 → 实际列名
        const nameCol = findCol(['名称','经销商名称','客户名称','公司名称','终端名称','医院名称','药店名称']);
        const drugCol = findCol(['药品名称','药品','经销药品']);
        const levelCol = findCol(['经销商级别','级别','等级']);
        const typeCol = findCol(['类型','客户类型','终端类型']);
        const provinceCol = findCol(['省份','省','区域']);
        const codeCol = findCol(['编码','经销商编码','客户编码','代码','编号']);
        if (!nameCol) { message.error('未找到"名称"列'); return; }

        let skipped = 0; const dtos: any[] = [];
        for (const row of rows) {
          const name = (row[nameCol] || '').trim();
          if (!name) { skipped++; continue; }
          const prov = provinceCol ? (row[provinceCol] || '').trim() : '';
          const drug = drugCol ? (row[drugCol] || '').trim() : '';
          const lvlName = (levelCol ? (row[levelCol] || '').trim() : '');
          const typName = typeCol ? (row[typeCol] || '').trim() || (isDistributor ? '商业' : '医院') : (isDistributor ? '商业' : '医院');
          if (isDup(name, prov, drug, lvlName, typName)) { skipped++; continue; }
          dtos.push({ code: '', name, province: prov, drugName: drug, customerTypeName: typName,
            level: lvlName === '一级经销商' ? 1 : lvlName === '二级经销商' ? 2 : lvlName === '三级经销商' ? 3 : null });
        }
        if (dtos.length === 0) { message.warning(`无有效数据，跳过 ${skipped} 条`); return; }
        const BATCH = 500; let imported = 0;
        for (let i = 0; i < dtos.length; i += BATCH) {
          const res = await apiClient.post(`/customers/import?isEndCustomer=${!isDistributor}`, dtos.slice(i, i + BATCH));
          imported += (res as any).data || 0;
        }
        message.success(`导入完成: ${imported} 条` + (skipped > 0 ? `, 跳过 ${skipped} 条` : '')); load();
      } catch (ex: any) {
        message.error('Excel 解析失败: ' + (ex.message || '未知错误'));
      }
    };
    reader.onerror = () => message.error('文件读取失败');
    reader.readAsArrayBuffer(file);
    return false;
  };

  const distColumns: ColumnsType<CustomerDto> = [
    { title: '编码', dataIndex: 'code', width: 120 },
    { title: '名称', dataIndex: 'name', width: 200 },
    { title: '省份', dataIndex: 'province', width: 80,
      filters: PROVINCES.filter(p => p).map(p => ({ text: p, value: p })),
      onFilter: (v, r) => r.province === v,
    },
    { title: '药品名称', dataIndex: 'drugName', width: 140,
      filters: [...new Set(filteredData.map(d => d.drugName).filter(Boolean))].sort().map(n => ({ text: n, value: n })),
      onFilter: (v, r) => (r.drugName || '') === (v as string),
    },
    { title: '级别', dataIndex: 'levelName', width: 100,
      filters: DISTRIBUTOR_LEVELS.filter(l => l).map(l => ({ text: l, value: l })),
      onFilter: (v, r) => r.levelName === v,
    },
    ...(canWrite ? [{ title: '操作', width: 150, render: (_: any, r: any) => (
        <Space><Button size="small" onClick={() => handleEdit(r)}>编辑</Button>
          <Popconfirm title="确定删除？" onConfirm={() => handleDelete(r.id)}><Button size="small" danger>删除</Button></Popconfirm></Space>),
    }] : []),
  ];
  const endColumns: ColumnsType<CustomerDto> = [
    { title: '编码', dataIndex: 'code', width: 120 },
    { title: '名称', dataIndex: 'name', width: 200 },
    { title: '省份', dataIndex: 'province', width: 80,
      filters: PROVINCES.filter(p => p).map(p => ({ text: p, value: p })),
      onFilter: (v, r) => r.province === v,
    },
    { title: '药品名称', dataIndex: 'drugName', width: 140,
      filters: [...new Set(filteredData.map(d => d.drugName).filter(Boolean))].sort().map(n => ({ text: n, value: n })),
      onFilter: (v, r) => (r.drugName || '') === (v as string),
    },
    { title: '类型', dataIndex: 'customerTypeName', width: 100,
      filters: END_TYPES.map(t => ({ text: t, value: t })),
      onFilter: (v, r) => r.customerTypeName === v,
    },
    ...(canWrite ? [{ title: '操作', width: 150, render: (_: any, r: any) => (
        <Space><Button size="small" onClick={() => handleEdit(r)}>编辑</Button>
          <Popconfirm title="确定删除？" onConfirm={() => handleDelete(r.id)}><Button size="small" danger>删除</Button></Popconfirm></Space>),
    }] : []),
  ];

  return (
    <div>
      <h2 style={{ marginBottom: 16 }}>🏢 客户管理</h2>
      <Tabs activeKey={tab} onChange={k => { setTab(k); setPage(0); }} items={[
        { key: 'distributor', label: '经销商' }, { key: 'end', label: '终端客户' },
      ]} />
      <Space wrap style={{ marginBottom: 16 }}>
        <Input placeholder="客户名称/编码" value={search} onChange={e => setSearch(e.target.value)}
          onPressEnter={handleSearch} prefix={<SearchOutlined />} style={{ width: 220 }} allowClear />
        <Button type="primary" onClick={handleSearch}>搜索</Button>
        {canWrite && <Button type="primary" icon={<PlusOutlined />} onClick={handleAdd}>新增客户</Button>}
        {canWrite && <Upload accept=".xlsx" showUploadList={false} beforeUpload={handleExcelImport}>
          <Button icon={<UploadOutlined />}>导入Excel</Button>
        </Upload>}
        {canWrite && <Button danger icon={<DeleteOutlined />} onClick={async () => {
          if (window.confirm(`确定清空所有${isDistributor ? '经销商' : '终端客户'}？`)) {
            await apiClient.delete(`/customers/clear?type=${typeFilter}`); message.success('已清空'); load();
          }
        }}>清空</Button>}
      </Space>
      <Table rowKey="id" columns={isDistributor ? distColumns : endColumns} dataSource={data} loading={loading}
        onChange={(_p, _f, _s, extra) => { setFilteredCount(extra.currentDataSource.length); setFilteredData(extra.currentDataSource as CustomerDto[]); }}
        pagination={{ defaultPageSize: 10, showTotal: () => `共 ${filteredCount} 条`, showSizeChanger: true }} />
      <Modal title={editing ? '编辑客户' : '新增客户'} open={modalOpen} onOk={handleOk} onCancel={() => setModalOpen(false)}>
        <Form form={form} layout="vertical">
          <Form.Item name="name" label="客户名称" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="code" label="编码"><Input placeholder="留空自动生成" /></Form.Item>
          <Form.Item name="province" label="省份">
            <Select options={PROVINCES.map(p => ({ label: p || '请选择', value: p }))} /></Form.Item>
          <Form.Item name="drugName" label="药品名称">
            <Select showSearch allowClear placeholder="选择药品" filterOption={(v, o) => (o?.label as string || '').includes(v)}
              options={[...new Map(drugOptions.map(d => {
                const name = d.name.split(' [')[0];
                return [name, { label: name, value: name }];
              })).values()]} /></Form.Item>
          {isDistributor ? (
            <Form.Item name="levelName" label="经销商级别">
              <Select options={DISTRIBUTOR_LEVELS.map(l => ({ label: l || '无', value: l }))} /></Form.Item>
          ) : (
            <Form.Item name="customerTypeName" label="客户类型">
              <Select options={END_TYPES.map(t => ({ label: t, value: t }))} /></Form.Item>
          )}
        </Form>
      </Modal>
    </div>
  );
}
