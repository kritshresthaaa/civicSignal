from __future__ import annotations

import io
import os
import unittest

from fastapi import UploadFile

from app.main import (
    EmbeddingRequest,
    IncidentAnalysisRequest,
    IncidentMediaDescriptor,
    analyze_image,
    analyze_incident,
    create_embedding,
    transcribe_audio,
)


class AiServiceContractTests(unittest.IsolatedAsyncioTestCase):
    def setUp(self) -> None:
        os.environ["USE_HF_MODELS"] = "false"

    def test_embedding_contract_returns_requested_dimensions(self) -> None:
        response = create_embedding(EmbeddingRequest(text="large pothole near main street", dimensions=128))

        self.assertEqual(128, len(response.embedding))
        self.assertEqual("civicsignal-ai-service-hashing-embedding", response.modelName)
        self.assertEqual("0.1.0", response.modelVersion)

    def test_incident_analysis_uses_media_context(self) -> None:
        response = analyze_incident(
            IncidentAnalysisRequest(
                incidentId="incident-1",
                description="There is damage near the curb.",
                latitude=40.7128,
                longitude=-74.0060,
                media=[
                    IncidentMediaDescriptor(
                        id="media-1",
                        fileName="caller-audio.wav",
                        contentType="audio/wav",
                        storageUri="memory://audio",
                        mediaType="Audio",
                        analysisStatus="Succeeded",
                        transcript="Caller reports a large pothole in the road.",
                    )
                ],
            )
        )

        self.assertEqual("RoadDamage", response.category)
        self.assertTrue(any(item.title == "Audio transcript used" for item in response.evidence))

    async def test_audio_transcription_contract_falls_back_without_hf_models(self) -> None:
        upload = UploadFile(file=io.BytesIO(b"placeholder-audio"), filename="large-pothole.wav")

        response = await transcribe_audio(upload)

        self.assertIn("pothole", str(response["text"]).lower())
        self.assertEqual("not-loaded", response["modelVersion"])
        self.assertEqual("Audio", response["evidence"][0]["kind"])

    async def test_image_analysis_contract_falls_back_without_hf_models(self) -> None:
        upload = UploadFile(file=io.BytesIO(b"placeholder-image"), filename="streetlight-outage.jpg")

        response = await analyze_image(upload)

        self.assertEqual("Streetlight", response["labels"][0]["name"])
        self.assertEqual("not-loaded", response["modelVersion"])
        self.assertEqual("Image", response["evidence"][0]["kind"])


if __name__ == "__main__":
    unittest.main()
