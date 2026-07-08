---
title: Password Reset and Account Lockout
source: Article
---

## Self-Service Reset

Most password problems are resolved without IT. Users reset their own directory
password at `https://reset.corp.internal` after verifying identity with the
registered second factor. The new password propagates to email and VPN within a
few minutes; other systems may take up to one sync cycle (15 minutes).

## Lockout Policy

An account locks after five consecutive failed sign-ins and unlocks automatically
after 15 minutes. Manual unlock by the service desk is reserved for cases where
the user cannot wait — for example an on-call engineer during an incident. Record
the justification on the ticket when unlocking early.

## When to Escalate

Escalate to identity engineering only when self-service reset fails with a
directory error, when a user is locked out repeatedly within a short window
(a sign of a stale credential on another device), or when the second factor is
lost. A lost second factor requires in-person or video identity verification
before a reset — never reset it on the strength of an email request alone.
