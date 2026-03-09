
# Off the Grid – Hackathon Platform: Complete UI Recreation Prompt

## Overview
Recreate a full-featured internal hackathon management platform called "Off the Grid" for Blackbaud. The platform supports three user roles (Participant, Judge, Admin) with role-specific dashboards and navigation. It features idea submission, team formation, judging workflows, and admin configuration.

---

## 1. DESIGN SYSTEM & THEMING

### Fonts
- **Body**: Inter (weights 300–700) — loaded from Google Fonts
- **Display / Headings**: Plus Jakarta Sans (weights 500–800) — loaded from Google Fonts
- Headings (h1–h6) use the display font; everything else uses the body font.

### Color Palette (HSL values, CSS custom properties)

#### Light Mode
| Token | HSL |
|---|---|
| --background | 210 20% 98% |
| --foreground | 215 25% 15% |
| --card | 0 0% 100% |
| --card-foreground | 215 25% 15% |
| --primary (Blackbaud Blue) | 207 100% 26% |
| --primary-foreground | 0 0% 100% |
| --secondary | 210 20% 96% |
| --secondary-foreground | 215 25% 15% |
| --muted | 210 15% 93% |
| --muted-foreground | 215 15% 45% |
| --accent | 207 85% 94% |
| --accent-foreground | 207 100% 26% |
| --destructive | 0 72% 51% |
| --border | 214 20% 88% |
| --input | 214 20% 88% |
| --ring | 207 100% 26% |
| --success | 142 76% 36% |
| --warning | 38 92% 50% |
| --info | 199 89% 48% |
| --teal | 174 72% 56% |
| --cyan | 186 85% 75% |
| --lime | 82 77% 55% |

#### Dark Mode
| Token | HSL |
|---|---|
| --background | 215 30% 8% |
| --foreground | 210 20% 98% |
| --card | 215 28% 12% |
| --primary | 207 90% 54% |
| --secondary | 215 25% 18% |
| --muted | 215 25% 18% |
| --muted-foreground | 215 15% 65% |
| --accent | 207 50% 20% |
| --accent-foreground | 207 90% 70% |
| --destructive | 0 62% 50% |
| --border | 215 25% 22% |
| --success | 142 60% 45% |
| --warning | 38 80% 55% |
| --info | 199 80% 55% |

### Border Radius
- `--radius: 0.625rem` (10px)
- lg = var(--radius), md = calc(var(--radius) - 2px), sm = calc(var(--radius) - 4px)

### Gradients
- **gradient-primary**: 135deg from primary to slightly lighter primary
- **gradient-hero**: 135deg from deep primary (22% lightness) through mid primary to info-blue
- **gradient-card**: 180deg from white to near-white
- **gradient-accent**: 135deg from accent to slightly cyan accent
- **gradient-teal**: 135deg from cyan-85% through teal-72% to cyan again (used on auth page background)

### Shadows
- shadow-sm, shadow-md, shadow-lg (standard with foreground-based alpha)
- **shadow-glow**: 0 0 20px primary/15% — used on card hover states
- Cards use `hover:shadow-glow` and `hover:border-primary/30` transitions

### Custom Animations
- **fade-in**: opacity 0→1, translateY 10px→0, 0.3s ease-out
- **slide-up**: opacity 0→1, translateY 20px→0, 0.4s ease-out
- **scale-in**: opacity 0→1, scale 0.95→1, 0.2s ease-out
- **pulse-glow**: box-shadow pulses between 15px and 25px primary glow
- **confetti**: translateY 0→100vh with 720deg rotation, used on Winners page
- **spin-slow**: 360deg rotation over 8s, used on trophy decoration

### Prose Styles
Rich text content (`.prose`) has custom styles: heading sizes, list styles, link colors (primary with underline), and proper spacing. Used for FAQ rules, terms, and swag content rendered via `dangerouslySetInnerHTML`.

### Custom Scrollbar
Thin (8px) scrollbar with muted track and rounded thumb.

---

## 2. LAYOUT ARCHITECTURE

### App Shell
```
┌─────────────────────────────────────────────┐
│ Header (sticky, h-16, border-b)             │
│  [☰ mobile] [Logo + Title] ... [Role][🌙][👤]│
├──────────┬──────────────────────────────────┤
│ Sidebar  │ Main Content                     │
│ (w-64,   │ (flex-1, p-6 lg:p-8,            │
│  hidden  │  max-w-7xl, mx-auto)             │
│  <lg)    │                                  │
│          │                                  │
│  Nav     │                                  │
│  items   │                                  │
│  ...     │                                  │
│          │                                  │
│ ┌──────┐ │                                  │
│ │Help? │ │                                  │
│ │card  │ │                                  │
│ └──────┘ │                                  │
└──────────┴──────────────────────────────────┘
```

### Header
- **Sticky** at top, z-50, with `bg-card/95 backdrop-blur`
- Left: Mobile hamburger (lg:hidden) + Logo image (h-9 w-9 rounded-lg) + Title + "Hackathon Platform" subtitle
- Right: Role switcher dropdown (shows current role icon + label) + Dark mode toggle (Sun/Moon) + User avatar dropdown (avatar + name)
- User dropdown: Name, email, Profile link, Settings link, Sign Out (red with LogOut icon)
- Role switcher: Cycles between Participant (User icon, primary color), Judge (Gavel icon, warning color), Admin (Settings icon, success color)

### Sidebar (Desktop)
- Fixed left, w-64, border-r, bg-card, min-h-[calc(100vh-4rem)]
- Navigation items: icon + label, px-3 py-2.5, rounded-lg
- Active state: bg-primary text-primary-foreground
- Inactive: text-muted-foreground, hover:text-foreground hover:bg-accent
- Admin nav has collapsible children under "Settings" (General, Content, Tracks, Criteria, Awards, Judges, Users, Winners)
- Bottom: "Need Help?" card with gradient-accent background, links to FAQ

#### Nav Items by Role
**Participant**: Dashboard, Ideas, Teams, People, Winners, Swag, FAQ & Rules
**Judge**: Dashboard, Review Ideas, FAQ & Rules
**Admin**: Dashboard, Ideas, Teams, People, Winners, Analytics, Settings (with sub-items)

### Mobile Nav
- Sheet component sliding from left (w-72)
- Same nav items but larger touch targets (py-3)
- Includes "Need Help?" card at bottom

---

## 3. AUTH PAGE (/auth)

### Background
Full-screen gradient background simulating Blackbaud's brand aesthetic:
- Base: linear-gradient(135deg, hsl(186 85% 90%) → hsl(180 60% 85%) → hsl(186 85% 88%) → hsl(174 50% 80%) → hsl(186 85% 92%))
- Decorative floating shapes (absolute positioned, pointer-events-none):
  - Large teal radial gradient circle top-right (w-96 h-96, opacity-40)
  - Large cyan blob bottom-left (w-[500px] h-[500px], opacity-50)
  - Small diamond shapes (rotated 45deg squares, various teal/cyan/lime colors, opacity 40-70%)
  - Floating lime circle (w-16 h-16, hsl(82 77% 55%))
  - Sparkles icons scattered (from Lucide, text-primary/30)

### Card
- Centered vertically and horizontally (flex items-center justify-center)
- max-w-md, bg-card/95, backdrop-blur-sm, rounded-2xl, shadow-2xl, p-8 md:p-10, border border-border/30
- **Logo**: Blackbaud logo image (w-16 h-16 rounded-xl), centered
- **Title**: "Off the Grid" in display font, text-3xl md:text-4xl, bold
- **Year**: "2026" in text-2xl md:text-3xl, bold, text-primary
- **Subtitle**: "Sign in with your Blackbaud account to join the hackathon" in muted-foreground

### Blackbaud Login Button
- Full width, h-14, text-base font-semibold, bg-[#004f91] hover:bg-[#003d70], rounded-xl
- Custom globe SVG icon (24x24) or Loader2 spinner when loading
- Text: "Continue with Blackbaud" / "Connecting..."
- Hover: shadow-xl, scale-[1.02]; Active: scale-[0.98]

### Divider
- "or continue with email" text centered on a horizontal rule

### Email Form
- Email input + Password input (min 6 chars) + Submit button
- Toggle between "Sign In" / "Sign Up" modes via text link below
- Loading state shows Loader2 spinner

### Footer
- "By continuing, you agree to participate..." disclaimer
- "Powered by Blackbaud" branding below the card

---

## 4. PARTICIPANT DASHBOARD (/)

### Hero Section
- rounded-2xl, gradient-hero background, p-8, text-primary-foreground
- Optional lede image as absolute background (bg-cover bg-center opacity-30)
- Dark overlay gradient for text contrast
- Subtle SVG pattern overlay at 5% opacity
- Top-right: HackathonStatusBadge component (shows current phase)
- Title (text-3xl md:text-4xl font-bold) + Description
- Action buttons: "Spark an Idea" (translucent bg-primary-foreground/20) + date range badge + deadline countdown badge (destructive/20 if < 2 days)

### Stats Grid (4 columns on md+, 2 on mobile)
Each card has a gradient background from its semantic color:
1. **My Ideas** (primary gradient, Lightbulb icon)
2. **My Teams** (success gradient, Users icon)
3. **Total Teams** (warning gradient, Target icon)
4. **Participants** (info gradient, TrendingUp icon)
- Large number (text-3xl font-bold) + label (text-sm text-muted-foreground)
- Icon in corner (h-10 w-10, color/30 opacity)

### Quick Actions + Event Timeline (2-column grid on md+)
**Quick Actions card**:
- "Spark an Idea" (Sparkles icon), "Submit New Idea" (Lightbulb), "Browse Teams" (Users), "View All Ideas" (Trophy)
- Each is a full-width outline button with icon left, ChevronRight right

**Event Timeline card**:
- Vertical list of milestones with colored dots:
  - Completed: bg-success, shows ✓
  - Highlight (submission deadline, not past): bg-warning animate-pulse
  - Future: bg-muted
- Date formatted as "MMM d, yyyy h:mm a"

### Tracks Section
- Grid of track cards (md:2, lg:3 columns)
- Each card: border rounded-lg, bg-card, p-4
- Track image (if exists): w-12 h-12 thumbnail + larger h-32 preview
- Name (font-semibold) + tagline (text-xs text-primary) + description (text-sm text-muted-foreground)

### Judging Criteria Section
- Grid of criterion cards (md:2, lg:4 columns)
- Each: bordered card with warning/20 border
- Icon in warning/10 bg circle + name (font-semibold) + description (text-xs)

### Awards & Prizes Section
- Grid (md:2, lg:3) with success/20 borders
- Trophy icon + award name + prize text (text-xs text-success) + optional image + description

### Trending Ideas
- Header with "View All" ghost button linking to /ideas
- Grid of ProjectCards (md:2, lg:3)

---

## 5. JUDGE DASHBOARD (/)

### Hero Section
- Same gradient-hero with lede image as participant
- "Judge Dashboard" badge top-right (with Gavel icon)
- Personalized greeting: "Welcome, Judge [FirstName]!"
- Context-aware message about pending reviews
- "Start Reviewing" button if pending ideas exist

### Stats (3 columns)
- Assigned to Me (ClipboardCheck, primary), Pending (Clock, warning), Completed (CheckCircle2, success)
- Icon + large number + label

### Pending Reviews Card
- Lists assigned ideas not yet rated
- Each row: idea title + track name + "Review" button
- Empty states: "No ideas assigned yet" (AlertCircle icon) or "All reviews complete!" (CheckCircle2 icon)

### Judging Criteria Reference Card
- 2-column grid of criteria
- Each criterion: name, max score badge (primary/10), weight badge (warning/10), description, bullet points (up to 3)

---

## 6. ADMIN DASHBOARD (/)

### Hero Section
- Same gradient-hero with "Admin Dashboard" badge
- "Event Settings" and "Manage Judges" buttons

### Stats Grid (6 columns on lg, 4 on md, 2 on mobile)
- Participants (primary), Teams (info), All Ideas (warning), Submitted (success), Winners (destructive), Total Votes (muted)

### Engagement Overview Card
- 4-column grid: Ideas by Status (progress bars), Top Tracks (counts), Rating Progress (large number), Participation rate (percentage)

### Quick Action Cards (4-column grid)
- Event Settings, Manage Users, Judging Setup, Declare Winners
- Each: icon (h-8 w-8) + title + description, hover:shadow-glow

### Recent Submissions
- Grid of 3 most recent ProjectCards

---

## 7. IDEAS PAGE (/ideas)

### Header
- Title "Ideas Gallery" + "Submit Idea" button (Plus icon) + "Spark" icon button
- Filters row: Search input (with Search icon, pl-9), Status select, Track select, Grid/List toggle

### Tab Navigation
- For judges: "To Judge" tab (with Gavel icon + count) shown first
- Common tabs: All, My Ideas, Submitted, Winners — each with count

### Idea Grid
- Grid mode: md:2, lg:3 columns
- List mode: single column with spacing
- Empty state: centered text "No ideas found matching your criteria."

---

## 8. PROJECT CARD COMPONENT

### Structure
```
┌──────────────────────────────┐
│ Title (line-clamp-2)  [Status Badge] │
│ Track name badge                      │
├──────────────────────────────┤
│ Description (line-clamp-3)            │
│ [tag] [tag] [tag] [tag] +N            │
│ ── (if links exist) ──               │
│ 🔗 Repository  📹 Video  🌐 Demo     │
├──────────────────────────────┤
│ 👤 Author/Team    [👍 N] [💬 View]   │
│    "X ago"                            │
└──────────────────────────────┘
```
- hover:shadow-glow, hover:border-primary/30 transition
- Title links to /ideas/:id, hover:text-primary
- Tags: bg-accent text-accent-foreground rounded-full px-2 py-0.5
- Footer: avatar (h-7 w-7), author/team name, relative time, vote + view buttons

### Status Badge
- draft: bg-muted text-muted-foreground
- public: bg-primary/10 text-primary border-primary/20
- submitted: bg-warning/10 text-warning border-warning/20
- winner: bg-success/10 text-success border-success/20, with 🏆 emoji prefix
- archived: bg-muted text-muted-foreground opacity-60

---

## 9. IDEA DETAIL PAGE (/ideas/:id)

### Header
- Back arrow → /ideas
- Title + StatusBadge + "Assigned to you" badge (for judges)
- Vote button (outline, ThumbsUp + count)

### Owner View (Editable)
- Full edit form: title, description, track select, team select, tags (TagInput), video/repo/demo URLs
- Save as Draft + Submit for Judging buttons
- TOS checkbox required for submission

### Non-Owner View
- Description card with tags
- 2-column grid: Team/Author card + Timeline card (created, updated, submitted dates)
- Resources card (video, repo, demo links as outline buttons)
- Assigned Judges list (for admin view)

### Judge Rating Panel (for assigned judges)
- Per-criterion: name + description, score slider (0–maxScore), feedback textarea
- Overall feedback textarea
- Weighted total display (text-2xl font-bold text-primary)
- Submit/Update Rating button

### Admin Ratings Summary
- Shows all judge ratings with expandable per-criterion scores
- Average aggregate score

---

## 10. TEAMS PAGE (/teams)

### Header
- "Teams" title + "Create Team" button (Plus icon)
- Search input

### Create Team Dialog
- Name, Description (textarea), Skills (comma-separated) inputs
- "Create Team" button

### Team Cards Grid (md:2, lg:3)

---

## 11. TEAM CARD COMPONENT

### Structure
```
┌──────────────────────────────┐
│ Team Name [✏️]    [N pending] [Full] │
│ 👥 N/max members                     │
├──────────────────────────────┤
│ Description (line-clamp-2)            │
│ [skill] [skill] [skill]              │
│                                       │
│ Team Members                          │
│ 👤👤👤 (overlapping avatars)          │
│ Captain: Name                         │
├──────────────────────────────┤
│ 👑 You're the Captain                │
│ [⚙️ Manage] [📧 Invite] [🚪 Leave]  │
│ — OR —                               │
│ [Join Team] / [Cancel Request]        │
└──────────────────────────────┘
```

### Captain Management Dialog
- Tabs: Requests (approve/reject with ✓/✗ buttons), Invites (pending approvals), Send (email invite)
- Member join requests show avatar, name, email, optional message
- Invite by email with "They'll see the invite when they log in" helper text

---

## 12. WINNERS PAGE (/winners)

### Hero Section
- Gradient background (from-primary/20 via-warning/10 to-primary/5) with lede image
- Floating orbs (blur-3xl, animate-pulse) and confetti particles (20 pieces, staggered animation)
- Animated trophy: spinning outer ring (animate-spin-slow), golden gradient center, sparkle decorations
- "Winners Showcase" title with gradient text (from-foreground via-primary to-foreground)
- Stats row: Winners count, Tracks count, Awards count (separated by vertical dividers)

### Winners by Track
- Track header: image or icon box (w-16 h-16 rounded-2xl) + track name with Crown icon + tagline
- Track dividers: gradient horizontal lines with Sparkles icon
- Winner cards: ProjectCard + floating award badges (golden gradient circles, top-right corner) + award name tags below

### Empty State
- Dashed border card, Trophy icon in muted circle, "Winners Coming Soon!" message

---

## 13. FAQ PAGE (/faq)

- max-w-4xl centered
- Three cards: Rules (FileText icon), FAQ (HelpCircle icon, Accordion), Terms (BookOpen icon)
- Rules and Terms rendered as HTML via dangerouslySetInnerHTML with .prose styling
- FAQ: Accordion with questions as triggers, answers in muted-foreground

---

## 14. SWAG PAGE (/swag)

- max-w-4xl centered
- Single card with Shirt icon
- Content rendered as HTML via dangerouslySetInnerHTML with .prose styling
- Empty state: "Swag information coming soon!"

---

## 15. PEOPLE PAGE (/people)

- Title with total count
- Search input (by name, email, department)
- 3-column card grid (md:2, lg:3)
- Each card: Avatar (h-12 w-12) + name + email + department + role badge (color-coded: admin=destructive, judge=warning, participant=primary)
- Skills preview (first 3 as muted badges, "+N more")
- Empty state: UserCheck icon + message

---

## 16. PROFILE PAGE (/profile)

### Tabs: Profile, Avatar, Preferences

**Profile Tab**:
- Basic info card with avatar preview (h-20 w-20), name, email, role badges
- Name/email fields locked if Blackbaud-linked (with Lock icon + "Managed by Blackbaud")
- Department + Location inputs
- Blackbaud Profile card (if linked): environment, legal entity, title, job title, organization, phone, birthday, merchant accounts
- Skills card: badge list with X remove + autocomplete add input
- Interests card: same pattern as skills

**Avatar Tab**:
- DiceBear avatar generator with style selector (avataaars, bottts, personas, fun-emoji, adventurer, big-smile)
- Seed presets grid (Alex, Sarah, Michael, etc.)
- Custom URL input option
- Large preview (h-32 w-32)

**Preferences Tab**:
- Dark mode toggle with Sun/Moon icons
- Theme preview showing sample cards

---

## 17. ADMIN PAGE (/admin)

### 9-Tab Interface
Tabs: General, Content, Tracks, Criteria, Awards, Judges, Users, Winners

**General**: Event title, max team size, description, logo (ImageInput), lede image (ImageInput), milestones CRUD

**Content**: Rich text editors (TipTap) for Rules, FAQ (add/edit/remove Q&A pairs), Swag info, Terms & Conditions

**Tracks**: CRUD with name, tagline, description, icon picker (14 Lucide icons in grid), optional image upload

**Criteria**: CRUD with name, description, bullet points (dynamic list), max score, weight, icon picker

**Awards**: CRUD with name, description, icon picker, prize text, optional image upload

**Judges**: Idea assignment matrix — search/filter by track, bulk select ideas, assign judges via dropdown. Shows current assignments with unassign option. Rating summary per idea.

**Users**: User list with role management (select dropdown), ban/unban toggle per user. Shows avatar, name, email, current role.

**Winners**: Track-grouped idea lists with status toggles. Select ideas to mark as winners, assign awards via checkboxes.

---

## 18. NEW IDEA PAGE (/ideas/new)

- max-w-3xl centered
- Back arrow + title
- Form card: title*, description* (h-32 textarea), track select, team select (individual or user's teams), tags (TagInput with suggestions), video URL, repo URL, demo URL
- TOS checkbox (required for submission, links to /faq)
- "Save as Draft" (outline) + "Submit for Judging" (primary) buttons

---

## 19. ANALYTICS PAGE (/analytics) — Admin Only

- Stats grid (4 columns): Total Ideas, Teams, Participants, Judges
- 2-column chart layout:
  - Donut/Pie chart: Ideas by Status (Recharts PieChart with inner radius)
  - Bar chart: Team Sizes (Recharts BarChart with rounded corners)
- Colors: primary, secondary, accent, muted

---

## 20. SPARK AN IDEA DIALOG

- Modal dialog (sm:max-w-[500px], h-[600px])
- Chat interface with Sparkles icon header
- Messages: user messages (bg-primary, right-aligned) vs assistant messages (bg-muted, left-aligned)
- Input bar at bottom: text input + Send icon button
- AI streams responses via SSE from backend edge function
- When AI produces a JSON block with `ready_to_submit`, auto-creates idea as draft and navigates to it

---

## 21. COMPONENT LIBRARY

Uses shadcn/ui components throughout:
- Button (variants: default, secondary, outline, ghost, destructive; sizes: default, sm, lg, icon)
- Card (CardHeader, CardTitle, CardContent, CardFooter)
- Input, Textarea, Label
- Select (SelectTrigger, SelectValue, SelectContent, SelectItem)
- Dialog (DialogContent, DialogHeader, DialogTitle, DialogTrigger)
- Tabs (TabsList, TabsTrigger, TabsContent)
- Accordion (AccordionItem, AccordionTrigger, AccordionContent)
- Avatar (AvatarImage, AvatarFallback)
- Badge (variants: default, secondary, destructive, outline)
- Checkbox
- Slider
- Sheet (for mobile nav)
- DropdownMenu (DropdownMenuTrigger, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator)
- ScrollArea
- Tooltip
- Progress
- Separator

### Custom Components
- **StatusBadge**: Colored badge per idea status
- **HackathonStatusBadge**: Shows current event phase
- **ImageInput**: URL input + file upload to cloud storage (max 5MB)
- **RichTextEditor**: TipTap editor with toolbar (bold, italic, lists, links, images)
- **TagInput**: Comma-separated tag input with autocomplete suggestions
- **AutocompleteInput**: Text input with dropdown suggestions
- **LocationInput**: Location text input
- **SparkIdeaDialog**: AI chat dialog for brainstorming

### Icons
All icons from Lucide React library (lucide-react). Key icons used:
LayoutDashboard, Lightbulb, Users, Trophy, Gavel, Settings, HelpCircle, BarChart3, Shirt, UserCheck, Sparkles, Zap, Target, Heart, Code, Building, Cpu, Brain, Network, Crown, Award, Star, Medal, Plus, Search, Edit2, Trash2, Save, Send, ArrowLeft, ChevronRight, ChevronDown, Calendar, Clock, User, Moon, Sun, LogOut, Menu, X, Check, Loader2, ThumbsUp, MessageSquare, Github, Video, ExternalLink, FileText, BookOpen, Image, Ban, Lock, Globe, CreditCard, Phone, Briefcase, Building2, Camera, Palette, Hash, Mail, ClipboardCheck, CheckCircle2, AlertCircle, CheckSquare, Square, Grid3X3, List, Pencil, Download, Database, FileJson

---

## 22. RESPONSIVE BREAKPOINTS

- **Mobile** (<768px / md): Single column layouts, hamburger nav, condensed stat cards
- **Tablet** (md): 2-column grids, visible sidebar triggers
- **Desktop** (lg+): 3-column grids, persistent sidebar (w-64), expanded admin tabs

Container: max-w 1400px, centered, 2rem padding.

---

## 23. KEY INTERACTIONS & STATES

### Loading States
- Full page: centered Loader2 (h-8 w-8 animate-spin text-primary) on min-h-screen bg-background
- In-component: Loader2 inline with text
- Buttons: disabled + Loader2 spinner + "Loading..." text

### Empty States
- Centered layout with large muted icon (h-12 w-12), heading, description, optional action button
- Used on: Teams (no teams), Ideas (no matches), Winners (coming soon), People (no users)

### Toast Notifications
- Uses Sonner toast library
- Success (green), Error (red), Info toasts
- Common messages: "Idea submitted!", "Team created!", "Rating submitted!", "Settings saved!"

### Hover Effects
- Cards: shadow-glow + border-primary/30
- Buttons: standard variants + scale transforms on auth page
- Links: text-primary on hover
- All transitions: duration-300

### Dark Mode
- Toggle via Moon/Sun icon in header
- Toggles .dark class on document root
- All colors swap to dark mode palette
- Gradient adjustments for better contrast

---

## 24. DATA FLOW NOTES

### Authentication
- Dual auth: Blackbaud OAuth 2.0 (primary) + Email/Password (fallback)
- OAuth flow: edge function generates auth URL → redirect → callback edge function exchanges code for token → fetches user profile from SKY API → upserts profile → creates auth session
- Protected routes redirect to /auth if not logged in
- Banned users see a Ban icon + message screen

### Role System
- Roles stored in user_roles table (participant, judge, admin)
- Users can have multiple roles
- Role switcher in header allows switching between assigned roles
- Each role shows different navigation and dashboard

### State Management
- React Context (AppContext) holds all app state
- Data fetched from database via custom hooks (useHackathonData, useIdeas, useTeams, etc.)
- TanStack React Query for server state management

---

## 25. IMAGE ASSETS NEEDED

1. **blackbaud-logo.png** — Blackbaud company logo (used on auth page, ~64x64px display)
   - Download from the published app or use the official Blackbaud logo

All other images (track images, award images, lede/hero images, user avatars) are stored in cloud storage and referenced by URL in the database. User avatars default to DiceBear API generated SVGs.

---

## 26. SEED DATA

Use the JSON export from the /seed-data page of the existing app to populate your database. The export includes all tables with preserved UUIDs and foreign key relationships. Key remapping notes:
- user_id values are auth-system-specific — remap to your auth system's user IDs
- Image URLs point to the original storage bucket — re-upload and update URLs
- HTML content (rules, swag, terms, FAQ) is preserved as-is

Download the complete seed data from: [APP_URL]/seed-data
