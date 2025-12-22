# Tripppi – Architecture & Engineering Decisions

## Overview
Tripppi is a B2C flight booking platform built by a solo founder with a strong focus on:
- calm, predictable UX
- low operational cost
- minimal PII exposure
- clean, scalable architecture

This document captures **decisions**, not discussion, and is intended to be consumed by engineers and tools like GitHub Copilot.

---

## Product Scope (MVP)
- Flight search and booking (B2C)
- Guest checkout only (no user accounts)
- Payments handled entirely by Duffel
- North America focus initially

---

## High-Level Architecture

### Frontend
- **React SPA**
- Hosted on **Amazon S3** (private bucket)
- Served via **AWS CloudFront**
- No SSR required for MVP

### Backend
- **Monolithic backend (modular monolith)**
- Deployed on **ECS Fargate (Linux)**
- Behind **Application Load Balancer (ALB)**
- Stateless application instances

### Traffic Flow
```
User
 → CloudFront (edge)
   ├─ /        → S3 (React app)
   └─ /api/*   → ALB → ECS Fargate
```

CloudFront is the **only public entry point**.

---

## API Exposure & Security Model

- No public-facing API product
- Browser-accessible APIs only via CloudFront
- ALB is NOT directly accessible from the internet

### Protections
- CloudFront + AWS WAF
- ALB security group allows inbound ONLY from CloudFront
- Custom origin header (e.g. `X-Origin-Secret`) validated by backend
- CORS restricted to Tripppi domain
- Rate limiting at WAF + application level

---

## Duffel Integration (Payments & Booking)

### Key Principle
> Tripppi never handles or stores payment card data.

- Duffel handles all payments
- No PAN, CVV, or card metadata stored or logged

### Booking Flow (Simplified)
1. Place/airport search
2. Offer request
3. Offer selection & repricing
4. Passenger data collection
5. Order creation via Duffel
6. Confirmation + email

### Stored References
- Duffel `offer_id`
- Duffel `order_id`
- Airline booking reference (PNR, if provided)
- Payment status (paid / failed / pending)

---

## PII Handling Policy

### Do NOT Store
- Credit card data
- CVV / expiry / billing address
- Passport scans or numbers (persistently)
- Full DOB long-term

### Allowed (Minimal)
- Passenger first & last name
- Email (required for confirmation)
- Phone (only if airline requires)
- Itinerary summary

### Transient PII
- DOB / passport info (if required)
- Encrypted at rest
- Automatically purged after booking confirmation (7–30 days)

### Logging Rules
- Never log raw request/response payloads containing PII
- Log only IDs, statuses, and error codes

---

## Guest Checkout & Access Model

- No user accounts
- No passwords

### Booking Access
- Magic-link emailed to user
- Short-lived, single-use token
- Booking reference code for support

---

## Backend Design Principles

### Modular Monolith
Code is structured by **domain**, not layers:
- Search
- Offers / Pricing
- Booking
- Payments (integration only)
- Notifications
- Admin / Support

### Scalability
- Horizontal scaling via ECS
- Redis / cache for search results
- Background worker for:
  - emails
  - retries
  - webhooks

Microservices are intentionally deferred.

---

## Infrastructure Summary

- CloudFront: CDN + security perimeter
- S3: static frontend hosting
- ALB: internal routing only
- ECS Fargate: backend runtime
- WAF: rate limiting & bot protection

---

## Guiding Principles

- Prefer simplicity over premature optimization
- Minimize blast radius of data breaches
- Keep UX calm; handle failures gracefully
- Scale the monolith before splitting services

---

## Notes for Future

- SSR (Next.js) may be added later for SEO pages
- Partner/B2B APIs may justify API Gateway later
- Domain and branding can evolve independently of architecture

