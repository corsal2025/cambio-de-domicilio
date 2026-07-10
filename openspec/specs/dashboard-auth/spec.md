# dashboard-auth Specification

## Purpose

Authentication for the operator dashboard: no ASP.NET Identity, no external identity provider — one
`DashboardUser` table, PBKDF2 hashing, cookie sessions, and a minimal self-service password
recovery flow appropriate for a handful of internal users on a municipal LAN.

## Requirements

### Requirement: Password storage never uses reversible encryption
The system SHALL store only a PBKDF2 hash (SHA-256, 210,000 iterations) and a per-user random salt
— never the plaintext password, never a reversibly-encrypted form.

#### Scenario: Verifying a login attempt
- **WHEN** a login attempt is made
- **THEN** the system re-derives the hash from the submitted password and the stored salt/iteration
  count and compares in fixed time, never decrypting a stored value

### Requirement: Brute-force friction proportionate to a small internal user base
The system SHALL add a fixed delay on every failed login attempt and lock the account for 15 minutes
after 5 consecutive failures. This is deliberately not a full rate-limiter (e.g. no IP-based
throttling) — proportionate to a handful of known internal operators, not a public-facing service.

#### Scenario: Fifth consecutive failure locks the account
- **WHEN** a user fails to log in 5 times in a row
- **THEN** the account is locked for 15 minutes, and even the correct password is rejected with a
  "locked" outcome (not "invalid credentials") until the lock expires

#### Scenario: Successful login resets the counter
- **WHEN** a login succeeds
- **THEN** the failed-attempt counter and any lock are cleared

### Requirement: Self-service password change requires the current password
An authenticated user SHALL be able to change their own password from the dashboard by supplying
their current password, a new password (minimum 8 characters), and a matching confirmation.

#### Scenario: Correct current password and valid new password
- **WHEN** the user submits the correct current password plus a valid, matching new password
- **THEN** the password hash is updated and the user can log in with the new password immediately

#### Scenario: Wrong current password
- **WHEN** the submitted current password does not match
- **THEN** the change is rejected and the old password remains active

### Requirement: Optional recovery email, set by the user themselves
The system SHALL let an authenticated user set or update their own recovery email address from the
dashboard (the same page as password change). This field starts empty for every user (including
ones created via the `--add-user` CLI) and is never required.

#### Scenario: User sets their recovery email
- **WHEN** an authenticated user submits a validly-shaped email address
- **THEN** it is saved as their recovery email for future "forgot password" requests

### Requirement: Forgot-password flow never reveals account existence
The "¿Olvidaste tu contraseña?" flow SHALL show the same generic confirmation message regardless of
whether the submitted username exists, and regardless of whether it has a recovery email on file —
timing and response shape must not let an unauthenticated visitor distinguish "no such user" from
"user exists but has no recovery email" from "email sent".

#### Scenario: Unknown username
- **WHEN** a password-reset request is submitted for a username that doesn't exist
- **THEN** the same generic message is shown as for a successful request, and no email is sent

#### Scenario: Known username without a recovery email on file
- **WHEN** a password-reset request is submitted for a real user who never set a recovery email
- **THEN** the same generic message is shown, and no email is sent

#### Scenario: Known username with a recovery email on file
- **WHEN** a password-reset request is submitted for a user with a recovery email set
- **THEN** a single-use reset link is emailed to that address, and the same generic message is shown

### Requirement: Reset tokens are single-use and time-limited
A password-reset token SHALL expire 30 minutes after being issued and SHALL become unusable the
moment it is used to complete a reset. Requesting a new reset SHALL invalidate any previous
outstanding token for that user, so only the most recently emailed link is ever valid.

#### Scenario: Reusing an already-used token
- **WHEN** a token that already completed a password reset is submitted again
- **THEN** the reset is rejected as "already used"

#### Scenario: Requesting a second reset invalidates the first
- **WHEN** a user requests a password reset twice in a row
- **THEN** the token from the first email no longer completes a reset, even if unused and unexpired

#### Scenario: Expired token
- **WHEN** a token is submitted more than 30 minutes after being issued
- **THEN** the reset is rejected as invalid/expired, with guidance to request a new one

### Requirement: Completing a reset requires a valid new password
The system SHALL enforce the same minimum-length rule (8 characters) on a password-reset completion
as on a self-service change, and SHALL NOT consume the token if the new password is rejected for
being too short — the token remains usable for a subsequent valid attempt.

#### Scenario: New password too short
- **WHEN** the submitted new password is under 8 characters
- **THEN** the reset is rejected and the token remains valid for a later attempt with a proper
  password
