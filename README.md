# llm64 - 1000 Words

A web application that embeds text into PNG images at the base64 level, allowing text to appear verbatim in the base64-encoded output with proper line layout.

## Recent Updates

### Color Scheme and Navigation Update - Completed (2026-01-08)

**Changes Implemented:**
- Updated color scheme from blue/pink to "sand sea and sky" gradient theme
- Fixed base64Excerpt display to show first 50 lines (previously showed limited excerpt)
- Added "What is this?" page for concept explanation (placeholder content)
- Added navigation bar with links to Home and About pages
- Updated all UI colors to match new gradient theme:
  - Body background: Sand → Sea → Sky gradient (#e8d5c4 → #d2b48c → #87ceeb → #4a90a4 → #b0d4f1 → #e6f3ff)
  - Button gradients: Sand to sea tones
  - Upload section: Warm sand tones
  - Navigation: Ocean blue accent colors

**Navigation:**
- Sticky navigation bar at top with "1000 Words" branding
- Links: Home and "What is this?"
- Consistent styling with sand/sea/sky theme
- Responsive design for mobile

**Base64 Display Enhancement:**
- Now displays first 50 lines of base64 output
- Truncation indicator when more than 50 lines
- Better readability for reviewing embedded text

### UI Redesign - Completed (2026-01-08)

The Blazor WebAssembly application has been redesigned with an elegant tropical-themed interface:

**Features Implemented:**
- Bright and vibrant tropical island color palette with smooth gradients
- Fully responsive single-page application
- File upload with 5MB size limit and validation
- Text input with 5MB size limit and real-time size counter
- Two-column image comparison view (Original vs Modified)
- Image metadata display (resolution and file size in bytes)
- Download button for generated PNG images
- Expandable/collapsible section to view base64 header with 76-character line wrapping
- Smooth animations and transitions
- Modern card-based layout
- Error handling with visual feedback
- Loading states with spinner animation

**Image Comparison View:**
- Side-by-side display of original and modified images
- Headers: "Original" and "Modified"
- Metadata showing:
  - Image resolution (width × height)
  - File size in bytes with thousand separators
- Images constrained to max 300px height for compact display
- Responsive: stacks vertically on mobile devices

**Base64 Display:**
- Formatted at 76 characters per line (RFC 2045 standard for email)
- Shows how the image would appear when embedded in email
- Embedded text is visible in the base64 output

**Design Elements:**
- Purple to blue gradient background
- White frosted-glass card with backdrop filter
- Pink-to-coral gradient for the "Generate Image" button
- Blue gradient for the download button
- Elegant typography with gradient text effects on the title
- SVG icons for upload, download, and expand/collapse
- Responsive design for mobile and desktop
- Hover effects and transitions for better UX

**Title:**
- Application renamed to "1000 Words" (plays on "a picture is worth a thousand words")
- Tagline: "Because a picture is worth a thousand words... literally"

**File Size Limits:**
- PNG upload: 5MB maximum
- Text input: 5MB maximum
- Real-time validation and error messages

**Technical Updates:**
- Updated `index.html` title to "1000 Words"
- Added viewport meta tag for responsive design
- Redesigned `Index.razor` with new component structure
- Updated `app.css` with modern gradient background
- Removed unused using directives from `PngTextEmbedder.cs`
- Added helper methods:
  - `GetImageDimensions()`: Extracts width/height from PNG IHDR chunk
  - `FormatBase64WithLineBreaks()`: Wraps base64 at 76 characters per line
- Build passes with 0 warnings, 0 errors

The application is ready to use with its new elegant interface.

## Deployment

### GitHub Actions - S3 Deployment

The project includes an automated CI/CD pipeline that builds, tests, and deploys the Blazor WASM application to Amazon S3.

**Workflow File:** `.github/workflows/deploy-s3.yml`

**Triggers:**
- Automatic deployment on push to `release` or `staging` branches
- Manual deployment via workflow_dispatch with customizable base href

**Branch Deployment Paths:**
- `release` branch → deploys to `/1000-words/` subdirectory
- `staging` branch → deploys to `/1000-words-staging/` subdirectory
- `main` branch → does not trigger automatic deployment

**Features:**
- Runs all unit tests before deployment
- Builds and publishes Blazor WASM app with specified base href
- Syncs files to S3 with proper cache headers:
  - Static assets: `max-age=31536000, immutable`
  - HTML files: `max-age=0, must-revalidate`
- Invalidates CloudFront cache for updated content
- Provides deployment summary with environment details

**Required GitHub Secrets:**
- `AWS_ACCESS_KEY_ID` - AWS access key for S3 deployment
- `AWS_SECRET_ACCESS_KEY` - AWS secret key
- `AWS_REGION` - AWS region (e.g., us-east-1)
- `AWS_S3_BUCKET_NAME` - Target S3 bucket name
- `AWS_CLOUDFRONT_DISTRIBUTION_ID` - (Optional) CloudFront distribution ID for cache invalidation

**Manual Deployment:**
1. Go to Actions tab in GitHub repository
2. Select "CI/CD - Test and Deploy to S3" workflow
3. Click "Run workflow"
4. Choose branch and enter custom base href (e.g., `/1000-words/`)
5. Select environment (production/staging)

The workflow ensures that only tested, validated code is deployed to production.
