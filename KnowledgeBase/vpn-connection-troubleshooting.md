---
title: VPN Connection Troubleshooting
source: FAQ
---

## Symptoms

The VPN client fails to connect, drops repeatedly, or authenticates but passes no
traffic. Users on the "Remote-Full" profile are affected most often after a
password change or a laptop that resumed from sleep on a foreign network.

## First Checks

Confirm the machine has working internet without the VPN. Open a browser and load
an external site. If that fails, the problem is the local network, not the VPN.
Then confirm the VPN gateway address is `vpn.corp.internal` and the profile is
"Remote-Full", not the deprecated "Remote-Split".

## Authentication Failures

An "authentication failed" error after a recent password change means the cached
credential is stale. Clear the saved credential in the client and sign in again
with the current directory password. Accounts locked after five failed attempts
unlock automatically after 15 minutes; do not file a ticket before that window
passes.

## Connects But No Traffic

If the tunnel establishes but nothing routes, the split-tunnel DNS is usually the
cause. Disconnect, set the profile to "Remote-Full" so all traffic uses the
corporate resolver, and reconnect. Persisting failures after a full-tunnel retry
warrant a ticket tagged `network` with the client log attached.
