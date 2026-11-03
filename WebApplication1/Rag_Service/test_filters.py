from vector_store import get_query_embedding, collection

q_vec = get_query_embedding("What is idempotency?")

# Test 1: $in filter across courses [1, 2]
res1 = collection.query(
    query_embeddings=[q_vec],
    n_results=2,
    where={"$and": [{"course_id": {"$in": [1, 2]}}, {"visibility": {"$eq": "student"}}]}
)
print("Test 1 ($in [1,2]): Found", len(res1["ids"][0]), "docs.")
for m in res1["metadatas"][0]:
    print("  Course:", m["course_id"], "| Source:", m["source_file"])

# Test 2: global without course filter (for instructor)
res2 = collection.query(query_embeddings=[q_vec], n_results=2)
print("Test 2 (global): Found", len(res2["ids"][0]), "docs.")
for m in res2["metadatas"][0]:
    print("  Course:", m["course_id"], "| Source:", m["source_file"])
