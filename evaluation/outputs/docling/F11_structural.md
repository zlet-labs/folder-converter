# Document Conversion Architecture

This web document serves as a benchmark for HTML structural interpretation into Markdown format.

## Engine Requirements

- Preserve heading hierarchies from h1 to h6
- Format ordered and unordered lists accurately
- Convert HTML tables into standard GFM tables

## Performance Overview

| Component   | Language    | Status     |
|-------------|-------------|------------|
| Desktop UI  | C# / WPF    | Production |
| Worker Host | Python 3.11 | Evaluation |

Project documentation is hosted at [Zlet Converter Repository](https://github.com/zlet-labs/zlet-converter) .