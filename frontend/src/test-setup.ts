import '@testing-library/jest-dom'

// jsdom does not implement ResizeObserver, which Radix UI primitives
// (RadioGroup, ScrollArea, Dialog, …) reference via @radix-ui/react-use-size.
// A no-op stub is sufficient for component tests — they assert on rendered
// markup and behaviour, not on observed element dimensions.
if (typeof globalThis.ResizeObserver === 'undefined') {
  globalThis.ResizeObserver = class ResizeObserver {
    observe(): void {}
    unobserve(): void {}
    disconnect(): void {}
  }
}
