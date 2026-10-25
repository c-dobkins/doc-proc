import { describe, expect, it } from 'vitest';
import { ApiError, handleResponse } from './client';

const json = (body: unknown, status: number) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

describe('handleResponse', () => {
  it('returns parsed JSON for successful responses', async () => {
    await expect(handleResponse(json({ id: 1 }, 200))).resolves.toEqual({ id: 1 });
  });

  it('returns undefined for 202 Accepted with an empty body', async () => {
    await expect(handleResponse(new Response(null, { status: 202 }))).resolves.toBeUndefined();
  });

  it('returns undefined for 204 No Content', async () => {
    await expect(handleResponse(new Response(null, { status: 204 }))).resolves.toBeUndefined();
  });

  it('uses a plain string error body as the message', async () => {
    const error = await handleResponse(json('File exceeds 50MB limit.', 400)).catch((e) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ message: 'File exceeds 50MB limit.', status: 400 });
  });

  it('uses the ProblemDetails title as the message', async () => {
    const error = await handleResponse(json({ title: 'Validation Error' }, 400)).catch((e) => e);

    expect(error).toMatchObject({ message: 'Validation Error', status: 400 });
  });

  it('falls back to the status code when the body is not JSON', async () => {
    const error = await handleResponse(new Response('oops', { status: 502 })).catch((e) => e);

    expect(error).toMatchObject({ message: 'Request failed (502)', status: 502 });
  });
});
