import { useMemo } from "react";
import { useSessionStoredState } from "../editor-ui/useSessionStoredState.js";
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

interface RecordEditorSessionState {
  readonly appTab: AppEditorTab;
  readonly appTokenGroup: string;
  readonly contentTab: string;
  readonly genericTab: "" | "general";
  readonly iconThemeTab: "" | "general" | "tokens";
  readonly navigationBarTab: "" | "general" | "config";
  readonly statusBarTab: "" | "general" | "config";
  readonly moduleDesignGroup: string;
  readonly moduleThemeTab: ModuleThemeTab;
  readonly screenTab: ScreenInstanceTab;
  readonly themeTab: ThemeEditorTab;
  readonly themeTokenGroup: string;
}

function editorSessionKey(tableId: string, recordId: string | undefined) {
  return `${tableId}:${recordId ?? ""}`;
}

function defaultEditorSessionState(tableId: string): RecordEditorSessionState {
  return {
    appTab: "",
    appTokenGroup: "",
    contentTab: "header",
    genericTab: "general",
    iconThemeTab: tableId === "icon_themes" ? "tokens" : "general",
    navigationBarTab: tableId === "navigation_bars" ? "config" : "general",
    statusBarTab: tableId === "status_bars" ? "config" : "general",
    moduleDesignGroup: "",
    moduleThemeTab: "",
    screenTab: "",
    themeTab: "",
    themeTokenGroup: "",
  };
}

export function useRecordEditorTabs({
  recordId,
  tableId,
}: RecordEditorTabsOptions) {
  const sessionKey = useMemo(
    () => editorSessionKey(tableId, recordId),
    [recordId, tableId],
  );
  const defaults = useMemo(
    () => defaultEditorSessionState(tableId),
    [tableId],
  );

  const [screenTab, setScreenTab] = useSessionStoredState(
    `${sessionKey}:screenTab`,
    defaults.screenTab,
  );
  const [contentTab, setContentTab] = useSessionStoredState(
    `${sessionKey}:contentTab`,
    defaults.contentTab,
  );
  const [appTab, setAppTab] = useSessionStoredState(
    `${sessionKey}:appTab`,
    defaults.appTab,
  );
  const [appTokenGroup, setAppTokenGroup] = useSessionStoredState(
    `${sessionKey}:appTokenGroup`,
    defaults.appTokenGroup,
  );
  const [themeTab, setThemeTab] = useSessionStoredState(
    `${sessionKey}:themeTab`,
    defaults.themeTab,
  );
  const [themeTokenGroup, setThemeTokenGroup] = useSessionStoredState(
    `${sessionKey}:themeTokenGroup`,
    defaults.themeTokenGroup,
  );
  const [moduleThemeTab, setModuleThemeTab] = useSessionStoredState(
    `${sessionKey}:moduleThemeTab`,
    defaults.moduleThemeTab,
  );
  const [moduleDesignGroup, setModuleDesignGroup] = useSessionStoredState(
    `${sessionKey}:moduleDesignGroup`,
    defaults.moduleDesignGroup,
  );
  const [iconThemeTab, setIconThemeTab] = useSessionStoredState(
    `${sessionKey}:iconThemeTab`,
    defaults.iconThemeTab,
  );
  const [statusBarTab, setStatusBarTab] = useSessionStoredState(
    `${sessionKey}:statusBarTab`,
    defaults.statusBarTab,
  );
  const [navigationBarTab, setNavigationBarTab] = useSessionStoredState(
    `${sessionKey}:navigationBarTab`,
    defaults.navigationBarTab,
  );
  const [genericTab, setGenericTab] = useSessionStoredState(
    `${sessionKey}:genericTab`,
    defaults.genericTab,
  );

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
