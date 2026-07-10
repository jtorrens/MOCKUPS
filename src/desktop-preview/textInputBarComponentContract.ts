import type { SpacingPairContract } from "./previewComponentContracts.js";
import type { IconBarDesignContract } from "./iconBarComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";
import type { TextBoxDesignContract } from "./textBoxComponentContract.js";

export interface TextInputBarDesignContract {
  id: string;
  availableWidth: number;
  height: number;
  barPadding: SpacingPairContract;
  barSurface: SurfaceDesignContract;
  iconGapToken: string;
  iconBar: IconBarDesignContract;
  textBox: TextBoxDesignContract;
}
