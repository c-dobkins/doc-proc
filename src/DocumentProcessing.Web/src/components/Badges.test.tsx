import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import ConfidenceBadge from './ConfidenceBadge';
import StatusBadge from './StatusBadge';

describe('ConfidenceBadge', () => {
  it.each([
    [95.4, '95%', 'bg-green-100'],
    [90, '90%', 'bg-green-100'],
    [89.6, '90%', 'bg-green-100'],
    [75, '75%', 'bg-yellow-100'],
    [42, '42%', 'bg-red-100'],
  ])('renders %s as %s with %s', (confidence, label, colorClass) => {
    render(<ConfidenceBadge confidence={confidence} />);

    expect(screen.getByText(label)).toHaveClass(colorClass);
  });
});

describe('StatusBadge', () => {
  it('humanizes NeedsReview', () => {
    render(<StatusBadge status="NeedsReview" />);

    expect(screen.getByText('Needs Review')).toBeInTheDocument();
  });

  it('renders other statuses verbatim', () => {
    render(<StatusBadge status="Approved" />);

    expect(screen.getByText('Approved')).toHaveClass('bg-emerald-100');
  });
});
