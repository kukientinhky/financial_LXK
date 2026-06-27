---
version: alpha
name: Financial LXK Dashboard
description: Design system for financial dashboard UI

colors:
  primary: "#1565C0"
  secondary: "#546E7A"
  accent: "#00A6A6"
  success: "#2E7D32"
  warning: "#F9A825"
  danger: "#C62828"
  background: "#F5F7FA"
  surface: "#FFFFFF"
  text: "#1A1C1E"
  muted: "#6C7278"
  border: "#E0E0E0"
  on-primary: "#FFFFFF"
  on-accent: "#FFFFFF"

typography:
  h1:
    fontFamily: Inter
    fontSize: 2rem
    fontWeight: 700
    lineHeight: 2.5rem
  h2:
    fontFamily: Inter
    fontSize: 1.5rem
    fontWeight: 600
    lineHeight: 2rem
  body:
    fontFamily: Inter
    fontSize: 1rem
    fontWeight: 400
    lineHeight: 1.5rem
  label:
    fontFamily: Inter
    fontSize: 0.875rem
    fontWeight: 500
    lineHeight: 1.25rem

rounded:
  sm: 4px
  md: 8px
  lg: 12px
  xl: 16px

spacing:
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 32px
  xxl: 48px

components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.on-primary}"
    typography: "{typography.label}"
    rounded: "{rounded.md}"
    padding: 12px

  button-accent:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.on-accent}"
    typography: "{typography.label}"
    rounded: "{rounded.md}"
    padding: 12px

  card:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text}"
    rounded: "{rounded.lg}"
    padding: 24px

  table-header:
    backgroundColor: "{colors.background}"
    textColor: "{colors.text}"
    typography: "{typography.label}"

  input:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text}"
    rounded: "{rounded.md}"
    padding: 12px
---

## Overview

Financial LXK uses a clean enterprise dashboard style.

The interface should feel simple, professional, and easy to scan. Avoid decorative UI. Prioritize readability, clear data hierarchy, and predictable layouts.

## Colors

Use primary blue for main actions, active navigation, and important highlights.

Use accent teal only for secondary highlights or positive visual emphasis.

Use danger red only for destructive actions or error states.

Use background for page-level layout and surface for cards, tables, forms, and modals.

## Typography

Use Inter for all UI text.

Headings should be clear and bold. Body text should stay readable. Labels should be slightly smaller and medium weight.

## Layout

Use consistent spacing.

Cards should use 24px padding. Page sections should be separated by 24px or 32px. Avoid cramped layouts.

## Shapes

Use medium radius for buttons and inputs. Use large radius for cards and modals.

## Components

Buttons must use defined component tokens.

Tables should be clean, readable, and suitable for financial data. Use clear headers, enough row spacing, and avoid excessive borders.

Forms should have clear labels, visible validation errors, and consistent input height.

## Do's and Don'ts

Do:
- Use tokens from this file.
- Keep dashboard UI clean.
- Make financial numbers easy to read.
- Use consistent spacing and radius.

Don't:
- Invent random colors.
- Use too many shadows.
- Mix different font families.
- Create crowded layouts.