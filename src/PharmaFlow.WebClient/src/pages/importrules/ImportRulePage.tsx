import { useState, useEffect, useCallback } from 'react';
import { Table, Button, Modal, Form, Input, Select, Switch, Space, Tag, message, Popconfirm } from 'antd';
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { useAuthStore } from '../../store/authStore';
import * as ruleApi from '../../api/importrules';
import type { ImportRuleDto, ModuleRuleConfigDto } from '../../api/importrules';

const MODULE_OPTIONS = [
  { label: '药品管理', value: 'Drug' },
  { label: '客户管理', value: 'Customer' },
  { label: '流向记录', value: 'DistributionRecord' },
  { label: '采购管理', value: 'Purchase' },
  { label: '销售管理', value: 'Sales' },
];

export default function ImportRulePage() {
  const canWrite = useAuthStore(s => s.canWrite);
  const [data, setData] = useState<ImportRuleDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<ImportRuleDto | null>(null);
  const [form] = Form.useForm();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await ruleApi.getRules({ page: 0, size: 99999 });
      setData(res.items);
    } catch (e: any) { message.error(e?.message || '加载失败'); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { load(); }, [load]);

  const handleAdd = () => { setEditing(null); form.resetFields(); form.setFieldsValue({ name: '', description: '', moduleConfigs: [] }); setModalOpen(true); };
  const handleEdit = (r: ImportRuleDto) => { setEditing(r); form.setFieldsValue({ ...r }); setModalOpen(true); };

  const handleOk = async () => {
    try {
      const values = await form.validateFields();
      if (editing) { await ruleApi.updateRule(editing.id, values); message.success('更新成功'); }
      else { await ruleApi.createRule(values); message.success('创建成功'); }
      setModalOpen(false); load();
    } catch (e: any) {
      if (e?.response?.data?.message) message.error(e.response.data.message);
      else if (e?.message && !e.errorFields) message.error(e.message);
    }
  };

  const handleDelete = async (id: string) => {
    try { await ruleApi.deleteRule(id); message.success('删除成功'); load(); }
    catch (e: any) { message.error(e?.message || '删除失败'); }
  };

  const columns: ColumnsType<ImportRuleDto> = [
    { title: '名称', dataIndex: 'name', width: 200 },
    { title: '描述', dataIndex: 'description', width: 200, ellipsis: true },
    {
      title: '模块', dataIndex: 'moduleConfigs', width: 300,
      render: (configs: ModuleRuleConfigDto[]) => configs?.map(c => (
        <Tag key={c.moduleName} color="blue">{MODULE_OPTIONS.find(o => o.value === c.moduleName)?.label || c.moduleName}</Tag>
      )) || '-',
    },
    ...(canWrite ? [{
      title: '操作', width: 120,
      render: (_: any, r: ImportRuleDto) => (
        <Space>
          <Button size="small" onClick={() => handleEdit(r)}>编辑</Button>
          <Popconfirm title="确定删除？" onConfirm={() => handleDelete(r.id)}>
            <Button size="small" danger>删除</Button>
          </Popconfirm>
        </Space>
      ),
    }] : []),
  ];

  return (
    <div>
      <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between' }}>
        <Button icon={<ReloadOutlined />} onClick={load}>刷新</Button>
        {canWrite && <Button type="primary" icon={<PlusOutlined />} onClick={handleAdd}>新建规则</Button>}
      </div>
      <Table columns={columns} dataSource={data} rowKey="id" loading={loading}
        scroll={{ x: 800 }} pagination={{ defaultPageSize: 10, showTotal: (t) => `共 ${t} 条`, showSizeChanger: true }} />

      <Modal title={editing ? '编辑导入规则' : '新建导入规则'} open={modalOpen} onOk={handleOk}
        onCancel={() => setModalOpen(false)} width={800} destroyOnClose>
        <Form form={form} layout="vertical">
          <Form.Item name="name" label="规则名称" rules={[{ required: true }]}>
            <Input placeholder="如：九州通流向导入规则" />
          </Form.Item>
          <Form.Item name="description" label="描述">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.List name="moduleConfigs">
            {(fields, { add, remove }) => (
              <>
                {fields.map(({ key, name, ...rest }) => (
                  <div key={key} style={{ border: '1px solid #d9d9d9', borderRadius: 8, padding: 12, marginBottom: 12, background: '#fafafa' }}>
                    <Space style={{ display: 'flex', marginBottom: 8 }} align="center">
                      <Form.Item {...rest} name={[name, 'moduleName']} label="目标模块" rules={[{ required: true }]} style={{ marginBottom: 0 }}>
                        <Select options={MODULE_OPTIONS} style={{ width: 140 }} />
                      </Form.Item>
                      <Form.Item {...rest} name={[name, 'autoCreate']} label="自动创建" valuePropName="checked" style={{ marginBottom: 0 }}>
                        <Switch />
                      </Form.Item>
                      <Button danger size="small" onClick={() => remove(name)}>删除</Button>
                    </Space>
                    <Form.Item {...rest} name={[name, 'fieldMappings']} label="字段映射 (JSON)" rules={[{ required: true }]} style={{ marginBottom: 8 }}>
                      <Input.TextArea rows={3} placeholder='{"Name": {"source":"FPRODUCTNAME"}, "Specification": {"source":"FSPECIFICATION"}}' />
                    </Form.Item>
                    <Form.Item {...rest} name={[name, 'normalizeRules']} label="规范化规则 (JSON)" style={{ marginBottom: 8 }}>
                      <Input.TextArea rows={2} placeholder='{"Specification":{"*":"×","calcMultiplier":true}}' />
                    </Form.Item>
                    <Form.Item {...rest} name={[name, 'matchConfig']} label="匹配配置 (JSON)" style={{ marginBottom: 0 }}>
                      <Input.TextArea rows={2} placeholder='{"matchFields":["Name","Specification"],"fuzzyThreshold":0.8}' />
                    </Form.Item>
                  </div>
                ))}
                <Button type="dashed" onClick={() => add({ moduleName: 'Drug', fieldMappings: '{}', autoCreate: false })} block>
                  + 添加模块配置
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </Modal>
    </div>
  );
}