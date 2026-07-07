import dayjs from 'dayjs';

export type DateRangeValue = [string, string] | null;

function parseRecordRange(value?: string) {
  if (!value) return null;
  const trimmed = value.trim();
  const isMonth = /^\d{4}-\d{2}$/.test(trimmed);
  const parsed = dayjs(isMonth ? `${trimmed}-01` : trimmed);
  if (!parsed.isValid()) return null;

  return {
    start: isMonth ? parsed.startOf('month') : parsed.startOf('day'),
    end: isMonth ? parsed.endOf('month') : parsed.endOf('day'),
  };
}

export function isDateInRange(value: string | undefined, range: DateRangeValue) {
  if (!range) return true;

  const recordRange = parseRecordRange(value);
  if (!recordRange) return false;

  const filterStart = dayjs(range[0]).startOf('day');
  const filterEnd = dayjs(range[1]).endOf('day');

  return recordRange.end.valueOf() >= filterStart.valueOf()
    && recordRange.start.valueOf() <= filterEnd.valueOf();
}
