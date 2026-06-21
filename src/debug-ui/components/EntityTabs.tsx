import type { AppRecord, AppTableDefinition } from "../api/client.js";

interface EntityTabsProps {
  tables: AppTableDefinition[];
  activeTableId: string;
  records: Record<string, AppRecord[]>;
  selectedRecordIds: Record<string, string>;
  onTableChange: (tableId: string) => void;
  onRecordSelect: (tableId: string, recordId: string) => void;
}

export function EntityTabs({
  tables,
  activeTableId,
  records,
  selectedRecordIds,
  onTableChange,
  onRecordSelect,
}: EntityTabsProps) {
  const activeTable = tables.find((table) => table.id === activeTableId);
  const activeRecords = activeTable ? (records[activeTable.id] ?? []) : [];

  return (
    <section className="panel entity-browser">
      <div className="tab-list" role="tablist">
        {tables.map((table) => (
          <button
            key={table.id}
            type="button"
            role="tab"
            data-testid={`tab-${table.id}`}
            className={table.id === activeTableId ? "active" : ""}
            onClick={() => onTableChange(table.id)}
          >
            {table.label}
            <span>{records[table.id]?.length ?? 0}</span>
          </button>
        ))}
      </div>
      <div className="record-list">
        {activeTable ? (
          activeRecords.length === 0 ? (
            <div className="empty-record-list">No records yet.</div>
          ) : (
            activeRecords.map((record) => (
              <button
                key={record.id}
                type="button"
                data-testid={`record-${record.id}`}
                className={
                  selectedRecordIds[activeTable.id] === record.id
                    ? "active"
                    : ""
                }
                onClick={() => onRecordSelect(activeTable.id, record.id)}
              >
                <strong>
                  {String(record[activeTable.titleColumn] ?? record.id)}
                </strong>
                <small>{record.id}</small>
              </button>
            ))
          )
        ) : null}
      </div>
    </section>
  );
}
