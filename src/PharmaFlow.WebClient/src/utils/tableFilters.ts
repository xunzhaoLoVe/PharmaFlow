export type TableFilterValues = Record<string, string[]>;

export interface TableFilterOption {
  label: string;
  value: string;
}

export interface TableFilterField<T> {
  key: string;
  label: string;
  getValue: (record: T) => unknown;
  options?: TableFilterOption[];
  width?: number;
}

export function applyTableFilters<T>(
  rows: T[],
  fields: TableFilterField<T>[],
  values: TableFilterValues,
) {
  return rows.filter(row =>
    fields.every(field => {
      const selected = values[field.key];
      if (!selected || selected.length === 0) return true;
      return selected.includes(String(field.getValue(row) ?? ''));
    }),
  );
}
