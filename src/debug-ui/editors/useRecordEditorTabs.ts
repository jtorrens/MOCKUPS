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
  const [contentTab, setContentTab] = useState("header");
  const [appTab, setAppTab] = useState<AppEditorTab>("");
  const [appTokenGroup, setAppTokenGroup] = useState("");
  const [themeTab, setThemeTab] = useState<ThemeEditorTab>("");
  const [themeTokenGroup, setThemeTokenGroup] = useState("");
  const [moduleThemeTab, setModuleThemeTab] = useState<ModuleThemeTab>("");
  const [moduleDesignGroup, setModuleDesignGroup] = useState("");
  const [iconThemeTab, setIconThemeTab] = useState<"" | "general" | "tokens">(
    "tokens",
  );
  const [statusBarTab, setStatusBarTab] = useState<"" | "general" | "config">(
    "config",
  );
  const [navigationBarTab, setNavigationBarTab] = useState<
    "" | "general" | "config"
  >("config");
  const [genericTab, setGenericTab] = useState<"" | "general">("general");

  useEffect(() => {
    setScreenTab("");
    setContentTab("header");
    setModuleThemeTab("");
    setAppTab("");
    setAppTokenGroup("");
    setThemeTab("");
    setThemeTokenGroup("");
    setModuleDesignGroup("");
    setIconThemeTab(tableId === "icon_themes" ? "tokens" : "general");
    setStatusBarTab(tableId === "status_bars" ? "config" : "general");
    setNavigationBarTab(
      tableId === "navigation_bars" ? "config" : "general",
    );
    setGenericTab("general");
  }, [recordId, tableId]);

  return {
    appTab,
    appTokenGroup,
    contentTab,
    genericTab,
    iconThemeTab,
    navigationBarTab,
    statusBarTab,
    moduleDesignGroup,
    moduleThemeTab,
    screenTab,
    setAppTab,
    setAppTokenGroup,
    setContentTab,
    setGenericTab,
    setIconThemeTab,
    setNavigationBarTab,
    setStatusBarTab,
    setModuleDesignGroup,
    setModuleThemeTab,
    setScreenTab,
    setThemeTab,
    setThemeTokenGroup,
    themeTab,
    themeTokenGroup,
  };
}
