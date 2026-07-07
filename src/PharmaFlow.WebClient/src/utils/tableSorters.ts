export function textSorter<T>(selector: (record: T) => unknown) {
  return (a: T, b: T) =>
    String(selector(a) ?? '').localeCompare(String(selector(b) ?? ''), 'zh-Hans-CN', {
      numeric: true,
      sensitivity: 'base',
    });
}

export function numberSorter<T>(selector: (record: T) => unknown) {
  return (a: T, b: T) => Number(selector(a) ?? 0) - Number(selector(b) ?? 0);
}
