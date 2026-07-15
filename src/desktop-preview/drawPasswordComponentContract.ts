export type DrawPasswordState = "initial" | "active" | "correct" | "incorrect";

export interface DrawPasswordDesignContract {
  id: string;
  state: DrawPasswordState;
  pattern: readonly number[];
  visibleCount: number;
  grid: { columns: number; rows: number };
  nodeSize: number;
  columnGapToken: string;
  rowGapToken: string;
  lineWidth: number;
  nodeColorToken: string;
  lineColorToken: string;
}
