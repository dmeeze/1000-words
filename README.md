# llm64 - 1000 Words

A web application that embeds text into PNG images at the base64 level, allowing text to appear verbatim in the base64-encoded output with proper line layout.

## Recent Updates

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
