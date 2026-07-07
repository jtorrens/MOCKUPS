import type { SpacingPairContract } from "./previewComponentContracts.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";
import type { TextBoxDesignContract } from "./textBoxComponentContract.js";

export interface TextInputBarDesignContract {
  id: string;
  height: number;
  barPadding: SpacingPairContract;
  barSurface: SurfaceDesignContract;
  textBox: TextBoxDesignContract;
}
