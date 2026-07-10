import { useMemo, useState } from 'react';
import { Badge, Button, Popover, Select, Space } from 'antd';
import { ClearOutlined, FilterOutlined } from '@ant-design/icons';
import { EMPTY_FILTER_VALUE, normalizeTableFilterValue, type TableFilterField, type TableFilterValues } from '../utils/tableFilters';

function buildOptions<T>(rows: T[], field: TableFilterField<T>) {
  const rowValues = rows.map(row => normalizeTableFilterValue(field.getValue(row)));
  const hasEmpty = rowValues.some(value => value === '');
  const baseOptions = field.options
    ? field.options
    : Array.from(new Set(rowValues.filter(Boolean)))
    .sort((a, b) => a.localeCompare(b, 'zh-Hans-CN', { numeric: true, sensitivity: 'base' }))
    .map(value => ({ label: value, value }));

  if (!hasEmpty || baseOptions.some(option => option.value === EMPTY_FILTER_VALUE || option.value === '')) {
    return baseOptions;
  }

  return [{ label: '空值', value: EMPTY_FILTER_VALUE }, ...baseOptions];
}

interface TableFiltersProps<T> {
  rows: T[];
  fields: TableFilterField<T>[];
  value: TableFilterValues;
  onChange: (value: TableFilterValues) => void;
}

export default function TableFilters<T>({ rows, fields, value, onChange }: TableFiltersProps<T>) {
  const [open, setOpen] = useState(false);
  const activeCount = Object.values(value).filter(items => items?.length > 0).length;
  const optionsMap = useMemo(
    () => Object.fromEntries(fields.map(field => [field.key, buildOptions(rows, field)])),
    [fields, rows],
  );

  const updateField = (key: string, next: string[]) => {
    const copy = { ...value };
    if (next.length > 0) copy[key] = next;
    else delete copy[key];
    onChange(copy);
  };

  const clear = () => onChange({});

  return (
    <Popover
      trigger="click"
      open={open}
      onOpenChange={setOpen}
      placement="bottomLeft"
      content={
        <Space direction="vertical" size={10} style={{ width: 280 }}>
          {fields.map(field => (
            <div key={field.key}>
              <div style={{ marginBottom: 4, color: '#475569', fontSize: 12 }}>{field.label}</div>
              <Select
                mode="multiple"
                allowClear
                showSearch
                maxTagCount="responsive"
                placeholder={`选择${field.label}`}
                value={value[field.key] || []}
                onChange={(next) => updateField(field.key, next)}
                options={optionsMap[field.key] || []}
                filterOption={(input, option) => String(option?.label ?? '').includes(input)}
                style={{ width: field.width || '100%' }}
              />
            </div>
          ))}
          <Space style={{ justifyContent: 'space-between', width: '100%' }}>
            <Button icon={<ClearOutlined />} onClick={clear} disabled={activeCount === 0}>重置</Button>
            <Button type="primary" onClick={() => setOpen(false)}>完成</Button>
          </Space>
        </Space>
      }
    >
      <Badge count={activeCount} size="small">
        <Button icon={<FilterOutlined />}>筛选</Button>
      </Badge>
    </Popover>
  );
}
