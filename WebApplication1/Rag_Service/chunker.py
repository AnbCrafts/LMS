"""
chunker.py
Extracts text from PDFs, text, or markdown files and performs 
recursive semantic chunking while attaching course & RBAC metadata.
"""

import os
from typing import List, Dict, Any
from pypdf import PdfReader


def extract_text_from_pdf(file_path: str) -> List[Dict[str, Any]]:
    """
    Extracts text page-by-page from a PDF to preserve page numbers for citations.
    """
    reader = PdfReader(file_path)
    pages_data = []
    
    for page_idx, page in enumerate(reader.pages):
        text = page.extract_text() or ""
        text = text.strip()
        if text:
            pages_data.append({
                "page_number": page_idx + 1,
                "text": text
            })
            
    return pages_data


def chunk_text(
    text: str, 
    chunk_size: int = 600, 
    chunk_overlap: int = 100
) -> List[str]:
    """
    Recursive sliding-window chunker.
    Keeps chunks around chunk_size characters with chunk_overlap to preserve context.
    Attempts to break cleanly at sentence ends or paragraph breaks.
    """
    if len(text) <= chunk_size:
        return [text.strip()] if text.strip() else []
        
    chunks = []
    start = 0
    text_len = len(text)
    
    while start < text_len:
        end = start + chunk_size
        
        # Try not to split in the middle of a sentence
        if end < text_len:
            # Look for paragraph break
            break_pos = text.rfind("\n", start, end)
            if break_pos == -1 or break_pos <= start:
                # Look for sentence end
                break_pos = text.rfind(". ", start, end)
            if break_pos != -1 and break_pos > start:
                end = break_pos + 1
                
        chunk = text[start:end].strip()
        if chunk:
            chunks.append(chunk)
            
        start = end - chunk_overlap
        if start >= text_len - chunk_overlap:
            break
            
    return chunks


def prepare_documents_for_indexing(
    file_path: str,
    course_id: int,
    visibility: str = "student",       # 'student' or 'instructor_only'
    doc_type: str = "lecture_notes"    # 'syllabus', 'lecture_notes', 'assignment', 'solution_key'
) -> List[Dict[str, Any]]:
    """
    Main ingestion function: Reads file, chunks text, and binds strict RBAC metadata.
    """
    if not os.path.exists(file_path):
        raise FileNotFoundError(f"Document not found: {file_path}")

    file_name = os.path.basename(file_path)
    ext = os.path.splitext(file_name)[1].lower()
    all_chunks = []

    if ext == ".pdf":
        pages = extract_text_from_pdf(file_path)
        for p in pages:
            page_chunks = chunk_text(p["text"])
            for chunk_idx, chunk in enumerate(page_chunks):
                all_chunks.append({
                    "text": chunk,
                    "metadata": {
                        "course_id": int(course_id),
                        "visibility": visibility,
                        "doc_type": doc_type,
                        "source_file": file_name,
                        "page_number": int(p["page_number"]),
                        "chunk_index": int(chunk_idx)
                    }
                })
    else:
        # Plain text, markdown, or code
        with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
            text = f.read()
            raw_chunks = chunk_text(text)
            for chunk_idx, chunk in enumerate(raw_chunks):
                all_chunks.append({
                    "text": chunk,
                    "metadata": {
                        "course_id": int(course_id),
                        "visibility": visibility,
                        "doc_type": doc_type,
                        "source_file": file_name,
                        "page_number": 1,
                        "chunk_index": int(chunk_idx)
                    }
                })

    return all_chunks
