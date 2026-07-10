import { useEffect, useState, useCallback } from 'react';
import { useLocation } from 'react-router-dom';
import { Table, Button, Space, Input, Modal, Form, message, Select, Upload } from 'antd';
import { PlusOutlined, SearchOutlined, UploadOutlined } from '@ant-design/icons';
import { useAuthStore } from '../../store/authStore';
import apiClient from '../../api/client';
import type { ColumnsType } from 'antd/es/table';
import * as XLSX from 'xlsx';
import { getCustomers, createCustomer } from '../../api/customers';
import { getDrugDropdown } from '../../api/drugs';
import TableFilters from '../../components/TableFilters';
import { applyTableFilters, type TableFilterField, type TableFilterValues } from '../../utils/tableFilters';
import { textSorter } from '../../utils/tableSorters';
import type { CustomerDto, DropdownItem } from '../../types';

const PROVINCES = ['','北京','天津','河北','山西','内蒙古','辽宁','吉林','黑龙江','上海','江苏','浙江','安徽','福建','江西','山东','河南','湖北','湖南','广东','广西','海南','重庆','四川','贵州','云南','西藏','陕西','甘肃','青海','宁夏','新疆'];
const DISTRIBUTOR_LEVELS = ['一级经销商','二级经销商','三级经销商'];
const DISTRIBUTOR_ROLES = ['商业公司','配送公司','代理商','其他'];
const distributorRoleToValue = (roleName?: string) => {
  switch (roleName) {
    case '商业公司': return 1;
    case '配送公司': return 2;
    case '代理商': return 3;
    case '其他': return 4;
    default: return null;
  }
};
const END_TYPES = ['医院','基层医疗机构','诊所','药店','其他'];
const normalizeEndType = (value?: string) => {
  const text = (value || '').trim();
  if (['卫生院','卫生服务中心','卫生室','卫生服务站'].includes(text)) return '基层医疗机构';
  if (text === '药房') return '药店';
  if (text === '门诊') return '诊所';
  if (END_TYPES.includes(text)) return text;
  return text || '医院';
};

export default function CustomerListPage() {
  const canWrite = useAuthStore(s => s.canWrite);
  const location = useLocation();
  const tab = location.pathname.endsWith('/end') ? 'end' : 'distributor';
  const [data, setData] = useState<CustomerDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [drugOptions, setDrugOptions] = useState<DropdownItem[]>([]);
  const [tableFilters, setTableFilters] = useState<TableFilterValues>({});
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

  useEffect(() => { getDrugDropdown().then(setDrugOptions).catch(() => {}); }, []);
  useEffect(() => { load(); }, [tab]);
  useEffect(() => { setTableFilters({}); }, [tab]);

  const handleSearch = () => { load(search); };
  const handleAdd = () => {
    form.resetFields();
    form.setFieldsValue({ customerTypeName: isDistributor ? '商业' : '医院', levelName: undefined, distributorRoleName: undefined });
    setModalOpen(true);
  };

  // 去重检查
  const isDup = (name: string, province: string, drugName: string, levelName?: string, typeName?: string, distributorRoleName?: string) => {
    return data.some(d => d.name === name && (d.province || '') === province && (d.drugName || '') === drugName &&
      (isDistributor
        ? ((d.levelName || '') === (levelName || '') && (d.distributorRoleName || '') === (distributorRoleName || ''))
        : ((d.customerTypeName || '') === (typeName || ''))));
  };

  const handleOk = async () => {
    const values = await form.validateFields();
    if (!isDistributor) values.customerTypeName = normalizeEndType(values.customerTypeName);
    else values.customerTypeName = '商业';
    const distributorRole = isDistributor ? distributorRoleToValue(values.distributorRoleName) : null;
    if (isDup(values.name, values.province || '', values.drugName || '', values.levelName, values.customerTypeName, values.distributorRoleName)) {
      message.warning('已存在相同名称、省份、药品名称、' + (isDistributor ? '机构角色、级别' : '类型') + '的客户'); return;
    }
    const dto = {
      ...values,
      level: values.levelName === '一级经销商' ? 1 : values.levelName === '二级经销商' ? 2 : values.levelName === '三级经销商' ? 3 : null,
      distributorRole,
      distributorRoleName: isDistributor ? values.distributorRoleName : '',
    };
    await createCustomer(dto);
    message.success('创建成功');
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

        const headers = Object.keys(rows[0]);
        const findCol = (aliases: string[]) => { for (const h of headers) { const t = h.trim(); if (aliases.includes(t)) return t; } return null; };

        // 建立列映射：别名 → 实际列名
        const nameCol = findCol(['名称','经销商名称','客户名称','公司名称','终端名称','医院名称','药店名称']);
        const drugCol = findCol(['药品名称','药品','经销药品']);
        const levelCol = findCol(['经销商级别','级别','等级']);
        const roleCol = findCol(['机构角色','角色','经销商角色','商业角色']);
        const typeCol = findCol(['类型','客户类型','终端类型']);
        const provinceCol = findCol(['省份','省','区域']);
        if (!nameCol) { message.error('未找到"名称"列'); return; }

        let skipped = 0; const dtos: any[] = [];
        for (const row of rows) {
          const name = (row[nameCol] || '').trim();
          if (!name) { skipped++; continue; }
          const prov = provinceCol ? (row[provinceCol] || '').trim() : '';
          const drug = drugCol ? (row[drugCol] || '').trim() : '';
          const lvlName = (levelCol ? (row[levelCol] || '').trim() : '');
          const roleName = isDistributor && roleCol ? (row[roleCol] || '').trim() : '';
          const rawTypeName = typeCol ? (row[typeCol] || '').trim() || (isDistributor ? '商业' : '医院') : (isDistributor ? '商业' : '医院');
          const typName = isDistributor ? '商业' : normalizeEndType(rawTypeName);
          if (isDup(name, prov, drug, lvlName, typName, roleName)) { skipped++; continue; }
          dtos.push({ code: '', name, province: prov, drugName: drug, customerTypeName: typName,
            distributorRole: distributorRoleToValue(roleName), distributorRoleName: roleName,
            level: lvlName === '一级经销商' ? 1 : lvlName === '二级经销商' ? 2 : lvlName === '三级经销商' ? 3 : null });
        }
        if (dtos.length === 0) { message.warning(`无有效数据，跳过 ${skipped} 条`); return; }
        const BATCH = 500; let imported = 0, supplemented = 0, duplicateSkipped = 0;
        for (let i = 0; i < dtos.length; i += BATCH) {
          const res = await apiClient.post(`/customers/import?isEndCustomer=${!isDistributor}`, dtos.slice(i, i + BATCH));
          const result = (res as any).data || { imported: 0, supplemented: 0, skipped: 0 };
          imported += result.imported || 0;
          supplemented += result.supplemented || 0;
          duplicateSkipped += result.skipped || 0;
        }
        message.success(`导入完成: 新增 ${imported} 条` + (supplemented > 0 ? `，补充 ${supplemented} 条` : '') + (duplicateSkipped > 0 ? `，跳过重复 ${duplicateSkipped} 条` : '') + (skipped > 0 ? `，跳过无效 ${skipped} 条` : '')); load();
      } catch (ex: any) {
        message.error('Excel 解析失败: ' + (ex.message || '未知错误'));
      }
    };
    reader.onerror = () => message.error('文件读取失败');
    reader.readAsArrayBuffer(file);
    return false;
  };

  const distColumns: ColumnsType<CustomerDto> = [
    { title: '编码', dataIndex: 'code', width: 120, sorter: textSorter<CustomerDto>(r => r.code) },
    { title: '名称', dataIndex: 'name', width: 200, sorter: textSorter<CustomerDto>(r => r.name) },
    { title: '省份', dataIndex: 'province', width: 80,
      sorter: textSorter<CustomerDto>(r => r.province),
    },
    { title: '药品名称', dataIndex: 'drugName', width: 140,
      sorter: textSorter<CustomerDto>(r => r.drugName),
    },
    { title: '机构角色', dataIndex: 'distributorRoleName', width: 110,
      sorter: textSorter<CustomerDto>(r => r.distributorRoleName),
      render: v => v || '-',
    },
    { title: '级别', dataIndex: 'levelName', width: 100,
      sorter: textSorter<CustomerDto>(r => r.levelName),
    },
  ];
  const endColumns: ColumnsType<CustomerDto> = [
    { title: '编码', dataIndex: 'code', width: 120, sorter: textSorter<CustomerDto>(r => r.code) },
    { title: '名称', dataIndex: 'name', width: 200, sorter: textSorter<CustomerDto>(r => r.name) },
    { title: '省份', dataIndex: 'province', width: 80,
      sorter: textSorter<CustomerDto>(r => r.province),
    },
    { title: '药品名称', dataIndex: 'drugName', width: 140,
      sorter: textSorter<CustomerDto>(r => r.drugName),
    },
    { title: '类型', dataIndex: 'customerTypeName', width: 100,
      sorter: textSorter<CustomerDto>(r => r.customerTypeName),
    },
  ];
  const filterFields: TableFilterField<CustomerDto>[] = [
    { key: 'province', label: '省份', getValue: r => r.province },
    { key: 'drugName', label: '药品名称', getValue: r => r.drugName },
    ...(isDistributor
      ? [
          { key: 'distributorRoleName', label: '机构角色', getValue: (r: CustomerDto) => r.distributorRoleName },
          { key: 'levelName', label: '级别', getValue: (r: CustomerDto) => r.levelName },
        ]
      : [
          { key: 'customerTypeName', label: '类型', getValue: (r: CustomerDto) => r.customerTypeName },
        ]),
  ];
  const tableData = applyTableFilters(data, filterFields, tableFilters);

  return (
    <div>
      <Space wrap style={{ marginBottom: 16 }}>
        <Input placeholder="客户名称/编码" value={search} onChange={e => setSearch(e.target.value)}
          onPressEnter={handleSearch} prefix={<SearchOutlined />} style={{ width: 220 }} allowClear />
        <Button type="primary" onClick={handleSearch}>搜索</Button>
        <TableFilters rows={data} fields={filterFields} value={tableFilters} onChange={setTableFilters} />
        {canWrite && <Button type="primary" icon={<PlusOutlined />} onClick={handleAdd}>新增客户</Button>}
        {canWrite && <Upload accept=".xlsx" showUploadList={false} beforeUpload={handleExcelImport}>
          <Button icon={<UploadOutlined />}>导入Excel</Button>
        </Upload>}
      </Space>
      <Table rowKey="id" columns={isDistributor ? distColumns : endColumns} dataSource={tableData} loading={loading}
        pagination={{ defaultPageSize: 10, showTotal: () => `共 ${tableData.length} 条`, showSizeChanger: true }} />
      <Modal title="新增客户" open={modalOpen} onOk={handleOk} onCancel={() => setModalOpen(false)} okText="确定" cancelText="取消" className="notranslate">
        <Form form={form} layout="vertical" className="notranslate">
          <Form.Item name="name" label="客户名称" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="code" label="编码"><Input placeholder="留空自动生成" /></Form.Item>
          <Form.Item name="province" label="省份">
            <Select allowClear options={PROVINCES.filter(Boolean).map(p => ({ label: p, value: p }))} /></Form.Item>
          <Form.Item name="drugName" label="药品名称">
            <Select showSearch allowClear placeholder="选择药品" filterOption={(v, o) => (o?.label as string || '').includes(v)}
              options={[...new Map(drugOptions.map(d => {
                const name = d.name.split(' [')[0];
                return [name, { label: name, value: name }];
              })).values()]} /></Form.Item>
          {isDistributor ? (
            <>
              <Form.Item name="distributorRoleName" label="机构角色">
                <Select allowClear>
                  {DISTRIBUTOR_ROLES.map(role => <Select.Option key={role} value={role}>{role}</Select.Option>)}
                </Select>
              </Form.Item>
              <Form.Item name="levelName" label="经销商级别">
                <Select allowClear>
                  {DISTRIBUTOR_LEVELS.map(level => <Select.Option key={level} value={level}>{level}</Select.Option>)}
                </Select>
              </Form.Item>
            </>
          ) : (
            <Form.Item name="customerTypeName" label="客户类型">
              <Select>
                {END_TYPES.map(type => <Select.Option key={type} value={type}>{type}</Select.Option>)}
              </Select>
            </Form.Item>
          )}
        </Form>
      </Modal>
    </div>
  );
}
