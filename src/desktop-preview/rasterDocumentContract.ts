export interface RasterDocumentSections {
  headHtml: string;
  bodyHtml: string;
}

export function rasterDocumentSections(html: string): RasterDocumentSections {
  const head = /<head(?:\s[^>]*)?>([\s\S]*?)<\/head>/i.exec(html);
  if (!head) throw new Error("Raster document head is unavailable");
  const body = /<body(?:\s[^>]*)?>([\s\S]*?)<\/body>/i.exec(html);
  if (!body) throw new Error("Raster document body is unavailable");
  return {
    headHtml: head[1],
    bodyHtml: body[1],
  };
}

export function rasterDocumentRequiresFullLoad(
  loadedViewport: string,
  loadedHeadHtml: string,
  nextViewport: string,
  nextHeadHtml: string,
) {
  return loadedViewport !== nextViewport || loadedHeadHtml !== nextHeadHtml;
}
