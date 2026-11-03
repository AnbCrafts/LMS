import os, io, json, pypdf
import google.generativeai as genai
from dotenv import load_dotenv

load_dotenv("D:/Anubhaw/Projects/Assignments/LMSPractice/WebApplication1/Rag_Service/.env")
genai.configure(api_key=os.getenv("GEMINI_API_KEY"), transport="rest")

reader = pypdf.PdfReader("D:/Anubhaw/Projects/Assignments/LMSPractice/WebApplication1/Uploads/CourseMaterials/Course2_ASPNET_MVC5_Enterprise_Architecture.pdf")
pdf_text = "\n".join([p.extract_text() for p in reader.pages if p.extract_text()])

prompt = f"""You are an expert curriculum designer and university examiner.
Generate exactly 3 high-quality multiple choice quiz questions based SOLELY on the lecture material below.

EACH QUESTION MUST HAVE:
- Question text (clear, testing conceptual understanding)
- 4 plausible options labeled A, B, C, D
- Correct option letter ('A', 'B', 'C', or 'D')
- A brief pedagogical explanation citing concepts from the text

OUTPUT FORMAT:
Return ONLY a valid JSON array of objects with these exact keys:
[
  {{
    "question_text": "...",
    "option_a": "...",
    "option_b": "...",
    "option_c": "...",
    "option_d": "...",
    "correct_option": "A",
    "explanation": "..."
  }}
]

LECTURE MATERIAL:
{pdf_text}
"""

model = genai.GenerativeModel("models/gemini-3.5-flash-lite")
resp = model.generate_content(prompt)
clean_text = resp.text.strip()
if clean_text.startswith("```json"):
    clean_text = clean_text[7:]
elif clean_text.startswith("```"):
    clean_text = clean_text[3:]
if clean_text.endswith("```"):
    clean_text = clean_text[:-3]

data = json.loads(clean_text.strip())
print(f"SUCCESS! Generated {len(data)} questions from PDF:")
for idx, q in enumerate(data, 1):
    print(f"\nQ{idx}: {q['question_text']}")
    print(f"  A) {q['option_a']}")
    print(f"  B) {q['option_b']}")
    print(f"  C) {q['option_c']}")
    print(f"  D) {q['option_d']}")
    print(f"  Correct: {q['correct_option']}")
    print(f"  Explanation: {q['explanation']}")
