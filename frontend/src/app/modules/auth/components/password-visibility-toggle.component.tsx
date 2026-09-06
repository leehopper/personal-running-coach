import { Eye, EyeOff } from 'lucide-react'

import { Button } from '@/components/ui/button'

export interface PasswordVisibilityToggleProps {
  isVisible: boolean
  onToggle: () => void
}

export const PasswordVisibilityToggle = ({
  isVisible,
  onToggle,
}: PasswordVisibilityToggleProps) => (
  <Button
    type="button"
    variant="ghost"
    size="icon"
    data-testid="password-visibility-toggle"
    className="absolute top-1/2 right-1 -translate-y-1/2"
    aria-label={isVisible ? 'Hide password' : 'Show password'}
    aria-pressed={isVisible}
    onClick={onToggle}
  >
    {isVisible ? <EyeOff aria-hidden="true" /> : <Eye aria-hidden="true" />}
  </Button>
)
