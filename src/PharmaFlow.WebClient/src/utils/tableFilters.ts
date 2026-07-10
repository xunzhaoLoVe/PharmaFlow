export type TableFilterValues = Record<string, string[]>;
export const EMPTY_FILTER_VALUE = '__TABLE_FILTER_EMPTY__';

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

export function normalizeTableFilterValue(value: unknown) {
  return String(value ?? '').trim();
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
      const rowValue = normalizeTableFilterValue(field.getValue(row));
      return selected.some(item => item === EMPTY_FILTER_VALUE ? rowValue === '' : item === rowValue);
    }),
  );
}
