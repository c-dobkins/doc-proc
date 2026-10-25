import { useEffect, useRef, useState, useCallback } from 'react';
import { getDocumentDownloadUrl } from '../api/client';
// pdf.js's default build targets only the newest browsers (it uses e.g. Map.getOrInsertComputed);
// the legacy build is the officially supported choice for broad browser compatibility.
import * as pdfjsLib from 'pdfjs-dist/legacy/build/pdf.mjs';

pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/legacy/build/pdf.worker.mjs',
  import.meta.url,
).toString();

interface Props {
  documentId: string;
  fileType: string;
  highlightText?: string | null;
}

const SCALE = 1.2;

interface PdfTextItem {
  str: string;
  transform: number[];
  width: number;
  height: number;
}

export default function PdfViewer({ documentId, fileType, highlightText }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const highlightCanvasRef = useRef<HTMLCanvasElement>(null);
  const [pageNum, setPageNum] = useState(1);
  const [numPages, setNumPages] = useState(0);
  const [loading, setLoading] = useState(true);
  const pdfDocRef = useRef<pdfjsLib.PDFDocumentProxy | null>(null);
  const textItemsRef = useRef<PdfTextItem[]>([]);
  const viewportRef = useRef<pdfjsLib.PageViewport | null>(null);

  const url = getDocumentDownloadUrl(documentId);
  const isPdf = fileType.toLowerCase() === 'pdf';

  const renderTaskRef = useRef<pdfjsLib.RenderTask | null>(null);

  useEffect(() => {
    if (!isPdf) {
      setLoading(false);
      return;
    }

    let cancelled = false;
    // Destroying the loading task also destroys its document (pdf.js 5+ has no PDFDocumentProxy.destroy)
    const loadingTask = pdfjsLib.getDocument({ url });
    loadingTask.promise
      .then((pdf) => {
        if (cancelled) return;
        pdfDocRef.current = pdf;
        setNumPages(pdf.numPages);
        return renderPage(pdf, 1);
      })
      .catch((err) => {
        if (!cancelled) console.error(err);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
      renderTaskRef.current?.cancel();
      renderTaskRef.current = null;
      loadingTask.destroy().catch(() => {});
      pdfDocRef.current = null;
    };
     
  }, [url, isPdf]);

  useEffect(() => {
    const pdf = pdfDocRef.current;
    if (pdf && pageNum >= 1 && pageNum <= numPages) {
      renderPage(pdf, pageNum);
    }
     
  }, [pageNum, numPages]);

  async function renderPage(pdf: pdfjsLib.PDFDocumentProxy, num: number) {
    renderTaskRef.current?.cancel();
    renderTaskRef.current = null;

    const page = await pdf.getPage(num);
    const canvas = canvasRef.current;
    const hlCanvas = highlightCanvasRef.current;
    if (!canvas) return;

    const viewport = page.getViewport({ scale: SCALE });
    viewportRef.current = viewport;
    canvas.height = viewport.height;
    canvas.width = viewport.width;
    if (hlCanvas) {
      hlCanvas.height = viewport.height;
      hlCanvas.width = viewport.width;
    }

    const task = page.render({ canvas, viewport });
    renderTaskRef.current = task;
    try {
      await task.promise;
    } catch {
      // Render was cancelled — safe to ignore
    }

    // Cache text content for highlighting
    const textContent = await page.getTextContent();
    textItemsRef.current = textContent.items
      .filter((item) => 'str' in item && (item as PdfTextItem).str.length > 0)
      .map((item) => item as unknown as PdfTextItem);
  }

  const drawHighlights = useCallback((searchText: string | null | undefined) => {
    const hlCanvas = highlightCanvasRef.current;
    const viewport = viewportRef.current;
    if (!hlCanvas || !viewport) return;

    const ctx = hlCanvas.getContext('2d');
    if (!ctx) return;

    // Clear previous highlights
    ctx.clearRect(0, 0, hlCanvas.width, hlCanvas.height);

    if (!searchText) return;

    const needle = searchText.toLowerCase().trim();
    if (!needle) return;

    const items = textItemsRef.current;
    let found = false;

    for (const item of items) {
      if (!item.str.toLowerCase().includes(needle)) continue;
      found = true;

      // Get position in PDF user space
      const pdfX = item.transform[4] ?? 0;
      const pdfY = item.transform[5] ?? 0;
      const fontHeight = item.transform[3] ?? 10; // scaleY = font size in PDF units

      // Convert baseline position to viewport coordinates
      const [vx, vy] = viewport.convertToViewportPoint(pdfX, pdfY);
      // Convert top-right corner to get proper width
      const [vx2] = viewport.convertToViewportPoint(pdfX + item.width, pdfY);
      const w = Math.abs(vx2 - vx);
      const h = fontHeight * SCALE;

      // Draw highlight (vy is the baseline, text goes upward)
      ctx.fillStyle = 'rgba(250, 204, 21, 0.4)';
      ctx.fillRect(vx, vy - h, w, h + 2);

      ctx.strokeStyle = 'rgba(234, 179, 8, 0.8)';
      ctx.lineWidth = 2;
      ctx.strokeRect(vx, vy - h, w, h + 2);
    }

    // If no exact match found, try matching individual words
    if (!found && needle.length > 3) {
      const words = needle.split(/\s+/).filter(w => w.length > 2);
      for (const item of items) {
        const lower = item.str.toLowerCase();
        if (!words.some(w => lower.includes(w))) continue;

        const pdfX = item.transform[4] ?? 0;
        const pdfY = item.transform[5] ?? 0;
        const fontHeight = item.transform[3] ?? 10;
        const [vx, vy] = viewport.convertToViewportPoint(pdfX, pdfY);
        const [vx2] = viewport.convertToViewportPoint(pdfX + item.width, pdfY);
        const w = Math.abs(vx2 - vx);
        const h = fontHeight * SCALE;

        ctx.fillStyle = 'rgba(250, 204, 21, 0.25)';
        ctx.fillRect(vx, vy - h, w, h + 2);
        ctx.strokeStyle = 'rgba(234, 179, 8, 0.5)';
        ctx.lineWidth = 1;
        ctx.strokeRect(vx, vy - h, w, h + 2);
      }
    }
  }, []);

  useEffect(() => {
    drawHighlights(highlightText);
  }, [highlightText, pageNum, drawHighlights]);

  if (!isPdf) {
    return (
      <div className="flex items-center justify-center rounded-lg border border-gray-200 bg-gray-50 p-4">
        <img
          src={url}
          alt="Document preview"
          className="max-h-[70vh] max-w-full object-contain"
        />
      </div>
    );
  }

  return (
    <div className="flex flex-col items-center gap-2">
      {loading && (
        <p className="py-10 text-sm text-gray-500">Loading PDF…</p>
      )}
      <div className="relative inline-block max-w-full">
        <canvas ref={canvasRef} className="max-w-full rounded border border-gray-200" />
        <canvas
          ref={highlightCanvasRef}
          className="pointer-events-none absolute left-0 top-0"
          style={{ width: '100%', height: '100%' }}
        />
      </div>
      {numPages > 1 && (
        <div className="flex items-center gap-3 text-sm">
          <button
            disabled={pageNum <= 1}
            onClick={() => setPageNum((p) => p - 1)}
            className="rounded bg-gray-200 px-2 py-1 text-gray-700 hover:bg-gray-300 disabled:opacity-40"
          >
            ← Prev
          </button>
          <span className="text-gray-600">
            {pageNum} / {numPages}
          </span>
          <button
            disabled={pageNum >= numPages}
            onClick={() => setPageNum((p) => p + 1)}
            className="rounded bg-gray-200 px-2 py-1 text-gray-700 hover:bg-gray-300 disabled:opacity-40"
          >
            Next →
          </button>
        </div>
      )}
    </div>
  );
}
