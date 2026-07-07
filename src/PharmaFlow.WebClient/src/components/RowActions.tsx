import type { ReactNode } from 'react';
import { Popconfirm } from 'antd';

export type RowActionTone = 'primary' | 'success' | 'warning' | 'default' | 'danger';

export interface RowActionItem {
  key: string;
  label: string;
  icon: ReactNode;
  onClick?: () => void | Promise<void>;
  confirmTitle?: ReactNode;
  tone?: RowActionTone;
  disabled?: boolean;
}

interface RowActionsProps {
  actions: Array<RowActionItem | false | null | undefined>;
}

export default function RowActions({ actions }: RowActionsProps) {
  return (
    <div className="pf-row-actions">
      {actions.filter(Boolean).map(action => {
        const item = action as RowActionItem;
        const button = (
          <button
            key={item.key}
            type="button"
            className={`pf-row-action pf-row-action-${item.tone ?? 'default'}`}
            onClick={item.confirmTitle ? undefined : item.onClick}
            disabled={item.disabled}
          >
            <span className="pf-row-action-icon">{item.icon}</span>
            <span className="pf-row-action-label">{item.label}</span>
          </button>
        );

        if (!item.confirmTitle || item.disabled) return button;

        return (
          <Popconfirm key={item.key} title={item.confirmTitle} onConfirm={item.onClick}>
            {button}
          </Popconfirm>
        );
      })}
    </div>
  );
}
