import { useEffect, useState } from "react";
import type {
  AppEditorTab,
  ModuleThemeTab,
  ScreenInstanceTab,
  ThemeEditorTab,
} from "./editorTabs.js";

interface RecordEditorTabsOptions {
  recordId: string | undefined;
  tableId: string;
}

export function useRecordEditorTabs({
  recordId,
  tableId,
}: RecordEditorTabsOptions) {
  const [screenTab, setScreenTab] = useState<ScreenInstanceTab>("");
  const [contentTab, setContentTab] = useState("participants");
  const [appTab, setAppTab] = useState<AppEditorTab>("");
  const [appTokenGroup, setAppTokenGroup] = useState("");
  const [themeTab, setThemeTab] = useState<ThemeEditorTab>("");
  const [themeTokenGroup, setThemeTokenGroup] = useState("");
  const [moduleThemeTab, setModuleThemeTab] = useState<ModuleThemeTab>("");
  const [moduleDesignGroup, setModuleDesignGroup] = useState("");
  const [genericTab, setGenericTab] = useState<"" | "general">("general");

  useEffect(() => {
    setScreenTab("");
    setContentTab("participants");
    setModuleThemeTab("");
    setAppTab("");
    setAppTokenGroup("");
    setThemeTab("");
    setThemeTokenGroup("");
    setModuleDesignGroup("");
    setGenericTab("general");
  }, [recordId, tableId]);

  return {
    appTab,
    appTokenGroup,
    contentTab,
    genericTab,
    moduleDesignGroup,
    moduleThemeTab,
    screenTab,
    setAppTab,
    setAppTokenGroup,
    setContentTab,
    setGenericTab,
    setModuleDesignGroup,
    setModuleThemeTab,
    setScreenTab,
    setThemeTab,
    setThemeTokenGroup,
    themeTab,
    themeTokenGroup,
  };
}
