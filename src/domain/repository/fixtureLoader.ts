import { InMemoryRepository } from "./InMemoryRepository.js";
import { createExampleDataset } from "./fixtures/exampleDataset.js";

export function loadExampleRepository(): InMemoryRepository {
  return new InMemoryRepository(createExampleDataset());
}
