import os
import google.generativeai as genai
from dotenv import load_dotenv

load_dotenv()
genai.configure(api_key=os.getenv("GEMINI_API_KEY"), transport="rest")

for m_name in ["models/gemini-3.1-flash-lite", "models/gemini-2.5-flash-lite"]:
    try:
        m = genai.GenerativeModel(m_name)
        r = m.generate_content("Hi in one word")
        print(m_name, "SUCCESS:", r.text.strip())
    except Exception as e:
        print(m_name, "FAILED:", e)
