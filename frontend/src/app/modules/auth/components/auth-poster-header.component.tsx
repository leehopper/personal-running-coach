import { Wordmark } from '~/modules/common/components/wordmark/wordmark.component'

export const AuthPosterHeader = () => (
  <header data-testid="auth-poster-header" className="flex flex-col gap-3">
    <Wordmark size="poster" />
    <div aria-hidden="true" className="h-0.5 w-16 bg-rule" />
    <p className="font-mono text-[13px] font-medium tracking-[0.1em] text-muted-foreground uppercase">
      The plan adapts. You do the work.
    </p>
  </header>
)
