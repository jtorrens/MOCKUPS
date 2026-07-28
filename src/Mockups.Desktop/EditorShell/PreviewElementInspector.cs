namespace Mockups.DesktopEditorShell.EditorShell;

internal static class PreviewElementInspector
{
    internal const string Markup =
        """
        <div aria-hidden="true" class="preview-element-highlight" id="previewElementHighlight"></div>
        <div aria-hidden="true" class="preview-element-label" id="previewElementLabel"></div>
        <aside aria-label="Elemento del preview" class="preview-element-inspector" id="previewElementInspector">
          <div class="preview-element-inspector-header">
            <span>Elemento del preview</span>
            <button aria-label="Cerrar identificación" class="preview-element-inspector-close" id="previewElementInspectorClose" type="button">×</button>
          </div>
          <div class="preview-element-inspector-caption">Ruta renderizada</div>
          <ol class="preview-element-inspector-path" id="previewElementInspectorPath"></ol>
        </aside>
        """;

    internal static string Css(bool isDark)
    {
        var panel = isDark ? "rgba(20,27,37,.97)" : "rgba(255,255,255,.98)";
        var text = isDark ? "#E8EEF8" : "#172033";
        var muted = isDark ? "#9FB1CA" : "#64748B";
        var border = isDark ? "rgba(148,163,184,.38)" : "rgba(100,116,139,.28)";
        var row = isDark ? "rgba(148,163,184,.10)" : "rgba(15,23,42,.045)";
        return $$"""
            .preview-element-highlight {
              position: absolute;
              z-index: 2200;
              display: none;
              border: 2px solid #2F80ED;
              border-radius: 3px;
              box-shadow: 0 0 0 1px rgba(255,255,255,.82), 0 5px 18px rgba(15,23,42,.24);
              pointer-events: none;
            }

            .preview-element-highlight.is-visible {
              display: block;
            }

            .preview-element-highlight.is-pinned {
              border-color: #F0B429;
            }

            .preview-element-label {
              position: absolute;
              z-index: 2201;
              display: none;
              max-width: min(420px, calc(100% - 24px));
              padding: 4px 7px;
              overflow: hidden;
              border-radius: 6px;
              background: rgba(15,23,42,.90);
              color: #F8FAFC;
              font: 650 11px/1.3 ui-monospace, SFMono-Regular, Menlo, monospace;
              text-overflow: ellipsis;
              white-space: nowrap;
              pointer-events: none;
            }

            .preview-element-label.is-visible {
              display: block;
            }

            .preview-element-inspector {
              position: absolute;
              z-index: 2300;
              display: none;
              width: min(390px, calc(100% - 24px));
              max-height: min(440px, calc(100% - 24px));
              overflow: hidden;
              padding: 0;
              border: 1px solid {{border}};
              border-radius: 12px;
              background: {{panel}};
              color: {{text}};
              box-shadow: 0 16px 38px rgba(15,23,42,.30);
            }

            .preview-element-inspector.is-visible {
              display: block;
            }

            .preview-element-inspector-header {
              display: flex;
              min-height: 42px;
              align-items: center;
              justify-content: space-between;
              gap: 12px;
              padding: 8px 10px 7px 13px;
              border-bottom: 1px solid {{border}};
              font-size: 13px;
              font-weight: 760;
            }

            .preview-element-inspector-close {
              display: grid;
              width: 28px;
              height: 28px;
              flex: 0 0 auto;
              place-items: center;
              padding: 0;
              border: 0;
              border-radius: 7px;
              background: transparent;
              color: inherit;
              font: 500 20px/1 sans-serif;
              cursor: pointer;
            }

            .preview-element-inspector-close:hover {
              background: {{row}};
            }

            .preview-element-inspector-caption {
              padding: 10px 13px 6px;
              color: {{muted}};
              font-size: 10px;
              font-weight: 760;
              letter-spacing: .08em;
              text-transform: uppercase;
            }

            .preview-element-inspector-path {
              display: grid;
              gap: 5px;
              max-height: 350px;
              margin: 0;
              padding: 0 10px 11px;
              overflow: auto;
              list-style: none;
            }

            .preview-element-inspector-path-item {
              display: grid;
              grid-template-columns: minmax(0, 1fr) auto;
              align-items: center;
              gap: 8px;
              min-height: 31px;
              padding: 6px 8px;
              border-radius: 7px;
              background: {{row}};
            }

            .preview-element-inspector-path-item.is-target {
              box-shadow: inset 3px 0 #F0B429;
            }

            .preview-element-inspector-id {
              overflow: hidden;
              font: 600 11px/1.35 ui-monospace, SFMono-Regular, Menlo, monospace;
              text-overflow: ellipsis;
              white-space: nowrap;
            }

            .preview-element-inspector-type {
              padding: 2px 5px;
              border: 1px solid {{border}};
              border-radius: 5px;
              color: {{muted}};
              font: 700 9px/1.2 ui-monospace, SFMono-Regular, Menlo, monospace;
            }
            """;
    }

    internal const string Script =
        """
        const previewElementHighlight = document.getElementById("previewElementHighlight");
        const previewElementLabel = document.getElementById("previewElementLabel");
        const previewElementInspector = document.getElementById("previewElementInspector");
        const previewElementInspectorPath = document.getElementById("previewElementInspectorPath");
        const previewElementInspectorClose = document.getElementById("previewElementInspectorClose");
        let previewElementTarget = null;
        let previewElementPinned = false;

        function previewRenderableElement(eventTarget, clientX, clientY) {
          const directElement = eventTarget instanceof Element
            ? eventTarget.closest("[data-renderable-id]")
            : null;
          if (directElement && scaleLayer.contains(directElement)) return directElement;
          if (!Number.isFinite(clientX) || !Number.isFinite(clientY)) return null;
          const candidates = [...scaleLayer.querySelectorAll("[data-renderable-id]")]
            .filter((element) => {
              const bounds = element.getBoundingClientRect();
              return bounds.width > 0
                && bounds.height > 0
                && clientX >= bounds.left
                && clientX <= bounds.right
                && clientY >= bounds.top
                && clientY <= bounds.bottom;
            });
          candidates.sort((left, right) => {
            if (left.contains(right)) return 1;
            if (right.contains(left)) return -1;
            const leftBounds = left.getBoundingClientRect();
            const rightBounds = right.getBoundingClientRect();
            return leftBounds.width * leftBounds.height - rightBounds.width * rightBounds.height;
          });
          return candidates[0] ?? null;
        }

        function previewRenderablePath(element) {
          const path = [];
          let current = element;
          while (current && current !== scaleLayer) {
            if (current.hasAttribute?.("data-renderable-id")) path.push(current);
            current = current.parentElement;
          }
          return path.reverse();
        }

        function previewRenderableLabel(element) {
          const id = element?.getAttribute?.("data-renderable-id") ?? "";
          const type = element?.getAttribute?.("data-renderable-type") ?? "";
          return type ? `${id} · ${type}` : id;
        }

        function hidePreviewElementHover() {
          if (previewElementPinned) return;
          previewElementTarget = null;
          previewElementHighlight?.classList.remove("is-visible", "is-pinned");
          previewElementLabel?.classList.remove("is-visible");
        }

        function positionPreviewElement(element, pinned = false) {
          if (!element?.isConnected || !previewElementHighlight || !previewElementLabel) {
            hidePreviewElementHover();
            return;
          }
          const bounds = element.getBoundingClientRect();
          const hostBounds = host.getBoundingClientRect();
          if (bounds.width <= 0 || bounds.height <= 0) {
            hidePreviewElementHover();
            return;
          }
          const left = bounds.left - hostBounds.left;
          const top = bounds.top - hostBounds.top;
          previewElementHighlight.style.left = `${left}px`;
          previewElementHighlight.style.top = `${top}px`;
          previewElementHighlight.style.width = `${bounds.width}px`;
          previewElementHighlight.style.height = `${bounds.height}px`;
          previewElementHighlight.classList.add("is-visible");
          previewElementHighlight.classList.toggle("is-pinned", pinned);

          previewElementLabel.textContent = previewRenderableLabel(element);
          previewElementLabel.classList.add("is-visible");
          const labelLeft = Math.max(6, Math.min(
            host.clientWidth - previewElementLabel.offsetWidth - 6,
            left,
          ));
          const labelAbove = top - previewElementLabel.offsetHeight - 5;
          const labelTop = labelAbove >= 6
            ? labelAbove
            : Math.min(host.clientHeight - previewElementLabel.offsetHeight - 6, top + bounds.height + 5);
          previewElementLabel.style.left = `${labelLeft}px`;
          previewElementLabel.style.top = `${Math.max(6, labelTop)}px`;
        }

        function closePreviewElementInspector() {
          previewElementPinned = false;
          previewElementTarget = null;
          previewElementInspector?.classList.remove("is-visible");
          previewElementHighlight?.classList.remove("is-visible", "is-pinned");
          previewElementLabel?.classList.remove("is-visible");
        }

        function openPreviewElementInspector(element, clientX, clientY) {
          if (!previewElementInspector || !previewElementInspectorPath) return;
          previewElementTarget = element;
          previewElementPinned = true;
          previewElementInspectorPath.replaceChildren();
          const path = previewRenderablePath(element);
          path.forEach((owner, index) => {
            const item = document.createElement("li");
            item.className = "preview-element-inspector-path-item";
            item.classList.toggle("is-target", index === path.length - 1);
            const id = document.createElement("span");
            id.className = "preview-element-inspector-id";
            id.textContent = owner.getAttribute("data-renderable-id") ?? "";
            id.title = id.textContent;
            const type = document.createElement("span");
            type.className = "preview-element-inspector-type";
            type.textContent = owner.getAttribute("data-renderable-type") ?? "node";
            item.append(id, type);
            previewElementInspectorPath.appendChild(item);
          });
          previewElementInspector.classList.add("is-visible");
          positionPreviewElement(element, true);

          const hostBounds = host.getBoundingClientRect();
          const requestedLeft = clientX - hostBounds.left + 8;
          const requestedTop = clientY - hostBounds.top + 8;
          const left = Math.max(8, Math.min(
            host.clientWidth - previewElementInspector.offsetWidth - 8,
            requestedLeft,
          ));
          const top = Math.max(8, Math.min(
            host.clientHeight - previewElementInspector.offsetHeight - 8,
            requestedTop,
          ));
          previewElementInspector.style.left = `${left}px`;
          previewElementInspector.style.top = `${top}px`;
        }

        function inspectPreviewElementHover(event) {
          if (previewElementPinned || isDragging) return;
          const element = previewRenderableElement(event.target, event.clientX, event.clientY);
          if (!element) {
            hidePreviewElementHover();
            return;
          }
          previewElementTarget = element;
          positionPreviewElement(element);
        }

        window.addEventListener("mousemove", inspectPreviewElementHover, { capture: true });
        window.addEventListener("mouseover", inspectPreviewElementHover, { capture: true });

        viewport.addEventListener("mouseleave", hidePreviewElementHover);

        function inspectPreviewElementFromRightClick(event) {
          const element = previewRenderableElement(event.target, event.clientX, event.clientY);
          if (!element) return;
          event.preventDefault();
          event.stopPropagation();
          event.stopImmediatePropagation();
          openPreviewElementInspector(element, event.clientX, event.clientY);
        }

        window.addEventListener("mousedown", (event) => {
          if (event.button === 2) inspectPreviewElementFromRightClick(event);
        }, { capture: true });
        window.addEventListener("mouseup", (event) => {
          if (event.button === 2 && previewRenderableElement(event.target, event.clientX, event.clientY)) {
            event.preventDefault();
            event.stopImmediatePropagation();
          }
        }, { capture: true });
        document.oncontextmenu = (event) => {
          if (!viewport.contains(event.target)) return true;
          event.preventDefault();
          event.stopPropagation();
          const element = previewRenderableElement(event.target, event.clientX, event.clientY);
          if (element) openPreviewElementInspector(element, event.clientX, event.clientY);
          return false;
        };

        previewElementInspectorClose?.addEventListener("click", closePreviewElementInspector);
        document.addEventListener("pointerdown", (event) => {
          if (!previewElementPinned || event.button !== 0) return;
          if (previewElementInspector?.contains(event.target)) return;
          closePreviewElementInspector();
        });
        document.addEventListener("keydown", (event) => {
          if (event.key === "Escape" && previewElementPinned) closePreviewElementInspector();
        });
        new ResizeObserver(() => {
          if (previewElementTarget?.isConnected) {
            positionPreviewElement(previewElementTarget, previewElementPinned);
          }
        }).observe(host);
        new MutationObserver(() => {
          if (previewElementTarget && !previewElementTarget.isConnected) {
            closePreviewElementInspector();
          }
        }).observe(scaleLayer, { childList: true, subtree: true });
        window.mockupsResetPreviewElementInspector = closePreviewElementInspector;
        """;
}
